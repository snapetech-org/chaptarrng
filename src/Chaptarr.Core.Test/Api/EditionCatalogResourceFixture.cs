using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Chaptarr.Api.V1.Books;
using Chaptarr.Http.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MediaFiles;
using CoreMediaCover = NzbDrone.Core.MediaCover.MediaCover;

namespace Chaptarr.Core.Test.Api
{
    [TestFixture]
    public class EditionCatalogResourceFixture
    {
        private class BookServiceProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IBookService.GetBook) && args[0] is int id)
                {
                    return new Book { Id = id };
                }

                throw new NotImplementedException(targetMethod?.Name);
            }
        }

        private class EditionServiceProxy : DispatchProxy
        {
            public List<Edition> Editions { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IEditionService.GetEditionsByBook) && args[0] is int)
                {
                    return Editions;
                }

                throw new NotImplementedException(targetMethod?.Name);
            }
        }

        private class MediaFileServiceProxy : DispatchProxy
        {
            public List<BookFile> Files { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IMediaFileService.GetFilesByBook) && args[0] is int)
                {
                    return Files;
                }

                throw new NotImplementedException(targetMethod?.Name);
            }
        }

        private class MediaCoverProxy : DispatchProxy
        {
            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IMediaCoverProxy.ProxyRemoteUrls))
                {
                    foreach (var cover in (IEnumerable<CoreMediaCover>)args[0])
                    {
                        cover.Url = "/MediaCoverProxy/edition-cover.jpg";
                    }

                    return null;
                }

                throw new NotImplementedException(targetMethod?.Name);
            }
        }

        [Test]
        public void should_return_a_sorted_edition_catalog_with_provider_ids_covers_and_ownership()
        {
            var editions = new List<Edition>
            {
                new Edition
                {
                    Id = 11,
                    Title = "Zulu",
                    Disambiguation = "Unabridged",
                    IsEbook = false,
                    NarratorNames = new List<string> { "Narrator One" },
                    DurationSeconds = 3240,
                    Publisher = "Audio House",
                    ReleaseDate = new DateTime(2025, 1, 2),
                    Images = new List<CoreMediaCover> { new CoreMediaCover(MediaCoverTypes.Cover, "https://covers.example/zulu.jpg") }
                },
                new Edition
                {
                    Id = 12,
                    Title = "Beta",
                    IsEbook = false,
                    Monitored = true,
                    IsGraphicAudio = true,
                    NarratorNames = new List<string> { "Narrator Two", "Narrator Three" },
                    HardcoverEditionId = "30643037"
                },
                new Edition
                {
                    Id = 13,
                    Title = "Alpha",
                    IsEbook = true,
                    Disambiguation = "Abridged",
                    NarratorNames = new List<string>(),
                    DurationSeconds = null
                }
            };

            var editionService = DispatchProxy.Create<IEditionService, EditionServiceProxy>();
            ((EditionServiceProxy)(object)editionService).Editions = editions;
            var mediaFileService = DispatchProxy.Create<IMediaFileService, MediaFileServiceProxy>();
            ((MediaFileServiceProxy)(object)mediaFileService).Files = new List<BookFile> { new BookFile { EditionId = 11 } };
            var coverProxy = DispatchProxy.Create<IMediaCoverProxy, MediaCoverProxy>();
            var bookService = DispatchProxy.Create<IBookService, BookServiceProxy>();

            var controller = new BookController(
                null, bookService, null, editionService, null, null, null, mediaFileService, null, coverProxy,
                null, null, null, null, null, null, null, null, null, null)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            controller.ControllerContext.HttpContext.Items[ReadarrFacadeContext.ItemKey] =
                new ReadarrFacadeContext("hc", "audiobook", "/readarr/hc/audiobook");

            var catalog = controller.GetEditionCatalog(10).Value;

            Assert.That(catalog.Select(edition => edition.EditionId), Is.EqualTo(new[] { 12, 13, 11 }));
            Assert.That(catalog[0].ForeignEditionId, Is.EqualTo("30643037"));
            Assert.That(catalog[0].Production, Is.EqualTo("full-cast"));
            Assert.That(catalog[0].Narrators, Is.EqualTo(new[] { "Narrator Two", "Narrator Three" }));
            Assert.That(catalog[0].Monitored, Is.True);
            Assert.That(catalog[0].Owned, Is.False);
            Assert.That(catalog[1].IsEbook, Is.True);
            Assert.That(catalog[1].Production, Is.EqualTo("unknown"));
            Assert.That(catalog[1].Abridged, Is.EqualTo("unknown"));
            Assert.That(catalog[2].Production, Is.EqualTo("solo"));
            Assert.That(catalog[2].Abridged, Is.EqualTo("unabridged"));
            Assert.That(catalog[2].DurationSeconds, Is.EqualTo(3240));
            Assert.That(catalog[2].Publisher, Is.EqualTo("Audio House"));
            Assert.That(catalog[2].ReleaseDate, Is.EqualTo(new DateTime(2025, 1, 2)));
            Assert.That(catalog[2].ImageUrl, Is.EqualTo("/MediaCoverProxy/edition-cover.jpg"));
            Assert.That(catalog[2].Owned, Is.True);
        }

        [Test]
        public void should_keep_ambiguous_production_unknown_and_split_legacy_narrator_text()
        {
            var edition = new Edition
            {
                Title = "Audio edition",
                Narrator = "Narrator A; Narrator B & Narrator A"
            };

            var resource = edition.ToCatalogResource(owned: false, facadeContext: null);

            Assert.That(resource.Production, Is.EqualTo("unknown"));
            Assert.That(resource.Abridged, Is.EqualTo("unknown"));
            Assert.That(resource.Narrators, Is.EqualTo(new[] { "Narrator A", "Narrator B" }));
        }
    }
}
