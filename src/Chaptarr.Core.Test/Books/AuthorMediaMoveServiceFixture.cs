using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Moq;
using NUnit.Framework;
using NLog;
using NzbDrone.Common;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Books;
using NzbDrone.Core.Books.Commands;
using NzbDrone.Core.Extras.Metadata.Files;
using NzbDrone.Core.Extras.Others;
using NzbDrone.Core.MediaFiles;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class AuthorMediaMoveServiceFixture
    {
        private readonly DateTime _modified = new DateTime(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
        private readonly Dictionary<string, (long Size, DateTime Modified)> _diskFiles = new(PathEqualityComparer.Instance);
        private readonly HashSet<string> _folders = new(PathEqualityComparer.Instance);
        private Mock<IAuthorService> _authorService;
        private Mock<IMediaFileService> _mediaFileService;
        private Mock<IMetadataFileService> _metadataFileService;
        private Mock<IOtherExtraFileService> _otherExtraFileService;
        private Mock<IDiskProvider> _diskProvider;
        private Mock<IDiskTransferService> _diskTransferService;
        private Mock<IRootFolderWatchingService> _rootFolderWatchingService;
        private AuthorMediaMoveService _service;
        private Author _author;
        private BookFile _ebook;
        private BookFile _audiobook;
        private MetadataFile _metadata;
        private OtherExtraFile _otherExtra;

        [SetUp]
        public void Setup()
        {
            _diskFiles.Clear();
            _folders.Clear();
            _folders.Add("/library");
            _folders.Add("/library/author");
            _folders.Add("/library/author/ebooks");
            _folders.Add("/library/author/audiobooks");
            _folders.Add("/archive");

            _author = new Author
            {
                Id = 7,
                Name = "Example Author",
                Path = "/library/author",
                EbookPath = "/library/author/ebooks",
                AudiobookPath = "/library/author/audiobooks"
            };

            _ebook = new BookFile
            {
                Id = 101,
                Path = "/library/author/ebooks/book.epub",
                Size = 12,
                Modified = _modified,
                MediaType = "ebook"
            };
            _audiobook = new BookFile
            {
                Id = 102,
                Path = "/library/author/audiobooks/book.m4b",
                Size = 24,
                Modified = _modified,
                MediaType = "audiobook"
            };
            _metadata = new MetadataFile
            {
                Id = 201,
                AuthorId = _author.Id,
                BookFileId = _ebook.Id,
                RelativePath = "metadata/cover.jpg"
            };
            _otherExtra = new OtherExtraFile
            {
                Id = 301,
                AuthorId = _author.Id,
                BookFileId = _ebook.Id,
                RelativePath = "extras/preview.jpg"
            };

            AddDiskFile(_ebook.Path, _ebook.Size);
            AddDiskFile(_audiobook.Path, _audiobook.Size);
            AddDiskFile("/library/author/metadata/cover.jpg", 5);
            AddDiskFile("/library/author/ebooks/extras/preview.jpg", 6);
            AddDiskFile("/library/author/notes.txt", 7);

            _authorService = new Mock<IAuthorService>();
            _authorService.Setup(service => service.GetAuthor(_author.Id)).Returns(_author);

            _mediaFileService = new Mock<IMediaFileService>();
            _mediaFileService.Setup(service => service.GetFilesByAuthor(_author.Id)).Returns(new List<BookFile> { _ebook, _audiobook });
            _mediaFileService.Setup(service => service.Get(_ebook.Id)).Returns(_ebook);
            _mediaFileService.Setup(service => service.Get(_audiobook.Id)).Returns(_audiobook);

            _metadataFileService = new Mock<IMetadataFileService>();
            _metadataFileService.Setup(service => service.GetFilesByAuthor(_author.Id)).Returns(new List<MetadataFile>
            {
                _metadata,
                new MetadataFile { Id = 202, AuthorId = _author.Id, RelativePath = "author-wide/metadata.xml" }
            });
            _metadataFileService.Setup(service => service.GetFilesByBookFile(_ebook.Id)).Returns(new List<MetadataFile> { _metadata });

            _otherExtraFileService = new Mock<IOtherExtraFileService>();
            _otherExtraFileService.Setup(service => service.GetFilesByAuthor(_author.Id)).Returns(new List<OtherExtraFile>
            {
                _otherExtra,
                new OtherExtraFile { Id = 302, AuthorId = _author.Id, RelativePath = "author-wide/extras.xml" }
            });
            _otherExtraFileService.Setup(service => service.GetFilesByBookFile(_ebook.Id)).Returns(new List<OtherExtraFile> { _otherExtra });

            _diskProvider = new Mock<IDiskProvider>();
            _diskProvider.Setup(provider => provider.FileExists(It.IsAny<string>()))
                .Returns((string path) => _diskFiles.ContainsKey(path));
            _diskProvider.Setup(provider => provider.FolderExists(It.IsAny<string>()))
                .Returns((string path) => _folders.Contains(path));
            _diskProvider.Setup(provider => provider.GetFileSize(It.IsAny<string>()))
                .Returns((string path) => _diskFiles[path].Size);
            _diskProvider.Setup(provider => provider.FileGetLastWrite(It.IsAny<string>()))
                .Returns((string path) => _diskFiles[path].Modified);
            _diskProvider.Setup(provider => provider.GetAvailableSpace(It.IsAny<string>())).Returns(1_000_000L);
            _diskProvider.Setup(provider => provider.GetMount(It.IsAny<string>())).Returns((IMount)null);

            _diskTransferService = new Mock<IDiskTransferService>();
            _diskTransferService.Setup(service => service.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move, false))
                .Returns((string source, string destination, TransferMode _, bool __) =>
                {
                    var entry = _diskFiles[source];
                    _diskFiles.Remove(source);
                    _diskFiles[destination] = entry;
                    _folders.Add(Path.GetDirectoryName(destination));
                    return TransferMode.Move;
                });

            _rootFolderWatchingService = new Mock<IRootFolderWatchingService>();

            _service = new AuthorMediaMoveService(
                _authorService.Object,
                _mediaFileService.Object,
                _metadataFileService.Object,
                _otherExtraFileService.Object,
                _diskProvider.Object,
                _diskTransferService.Object,
                _rootFolderWatchingService.Object,
                LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void should_preview_only_registered_files_for_the_selected_format_and_linked_sidecars()
        {
            var preview = _service.Preview(_author.Id, "ebook", "/archive/author");

            Assert.Multiple(() =>
            {
                Assert.That(preview.CanMove, Is.True);
                Assert.That(preview.Format, Is.EqualTo("ebook"));
                Assert.That(preview.MediaFileCount, Is.EqualTo(1));
                Assert.That(preview.SidecarFileCount, Is.EqualTo(2));
                Assert.That(preview.Files.Select(file => file.SourcePath), Is.EquivalentTo(new[]
                {
                    _ebook.Path,
                    "/library/author/metadata/cover.jpg",
                    "/library/author/ebooks/extras/preview.jpg"
                }));
                Assert.That(preview.Files.Any(file => file.SourcePath.Contains("notes.txt", StringComparison.Ordinal)), Is.False);
                Assert.That(preview.Files.Any(file => file.SourcePath.EndsWith(".m4b", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(preview.PreviewToken, Is.Not.Empty);
                Assert.That(_author.EbookPath, Is.EqualTo("/library/author/ebooks"), "preview must not change the configured path");
            });

            _mediaFileService.Verify(service => service.Update(It.IsAny<BookFile>()), Times.Never);
            _metadataFileService.Verify(service => service.Upsert(It.IsAny<MetadataFile>()), Times.Never);
        }

        [Test]
        public void should_block_the_move_if_any_destination_file_already_exists()
        {
            AddDiskFile("/archive/author/book.epub", _ebook.Size);

            var preview = _service.Preview(_author.Id, "ebook", "/archive/author");

            Assert.That(preview.CanMove, Is.False);
            Assert.That(preview.Conflicts, Is.Not.Empty);
        }

        [Test]
        public void should_build_a_bulk_preview_with_a_stable_batch_token()
        {
            var preview = _service.PreviewBatch(new[] { _author.Id }, "ebook", "/archive");

            Assert.Multiple(() =>
            {
                Assert.That(preview.CanMove, Is.True);
                Assert.That(preview.AuthorCount, Is.EqualTo(1));
                Assert.That(preview.MediaFileCount, Is.EqualTo(1));
                Assert.That(preview.DestinationRootPath, Is.EqualTo("/archive"));
                Assert.That(preview.Authors.Single().DestinationPath, Is.EqualTo("/archive/author"));
                Assert.That(preview.PreviewToken, Is.Not.Empty);
                Assert.That(_author.EbookPath, Is.EqualTo("/library/author/ebooks"), "preview must not change the configured path");
            });
        }

        [Test]
        public void should_move_only_previewed_files_and_update_their_catalog_paths()
        {
            var preview = _service.Preview(_author.Id, "ebook", "/archive/author");
            _author.EbookPath = preview.DestinationPath;
            _service.Execute(new MoveAuthorMediaCommand
            {
                AuthorId = _author.Id,
                Format = "ebook",
                SourcePath = preview.SourcePath,
                DestinationPath = preview.DestinationPath,
                PreviewToken = preview.PreviewToken,
                Files = preview.Files
            });

            Assert.Multiple(() =>
            {
                Assert.That(_ebook.Path, Is.EqualTo("/archive/author/book.epub"));
                Assert.That(_metadata.RelativePath, Is.EqualTo("cover.jpg"));
                Assert.That(_otherExtra.RelativePath, Is.EqualTo("preview.jpg"));
                Assert.That(_diskFiles.ContainsKey("/library/author/notes.txt"), Is.True, "untracked files stay in place");
                Assert.That(_diskFiles.ContainsKey(_audiobook.Path), Is.True, "the other format stays in place");
                Assert.That(_diskTransferService.Invocations.Count, Is.EqualTo(3));
            });

            _mediaFileService.Verify(service => service.Update(_ebook), Times.Once);
            _metadataFileService.Verify(service => service.Upsert(_metadata), Times.Once);
            _otherExtraFileService.Verify(service => service.Upsert(_otherExtra), Times.Once);
        }

        [Test]
        public void should_block_a_registered_sidecar_path_that_escapes_the_author_folders()
        {
            _otherExtra.RelativePath = "../outside.jpg";
            AddDiskFile("/library/author/outside.jpg", 6);

            var preview = _service.Preview(_author.Id, "ebook", "/archive/author");

            Assert.That(preview.CanMove, Is.False);
            Assert.That(preview.Conflicts, Has.Some.Contains("invalid path"));
            Assert.That(preview.Files.Any(file => file.SourcePath == "/library/author/outside.jpg"), Is.False);
        }

        [Test]
        public void should_stop_before_moving_a_source_that_changed_after_preview()
        {
            var preview = _service.Preview(_author.Id, "ebook", "/archive/author");
            _diskFiles[_ebook.Path] = (_ebook.Size + 1, _modified);

            Assert.Throws<IOException>(() => _service.Execute(new MoveAuthorMediaCommand
            {
                AuthorId = _author.Id,
                Format = "ebook",
                SourcePath = preview.SourcePath,
                DestinationPath = preview.DestinationPath,
                PreviewToken = preview.PreviewToken,
                Files = preview.Files
            }));

            _diskTransferService.Verify(service => service.TransferFile(It.IsAny<string>(), It.IsAny<string>(), TransferMode.Move, false), Times.Never);
            _mediaFileService.Verify(service => service.Update(It.IsAny<BookFile>()), Times.Never);
        }

        private void AddDiskFile(string path, long size)
        {
            _diskFiles[path] = (size, _modified);
        }
    }
}
