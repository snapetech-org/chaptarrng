using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using NzbDrone.Core.Books;
using NzbDrone.Core.Datastore;

namespace Chaptarr.Core.Test.Books
{
    [TestFixture]
    public class BookshelfLegacyIdentityMapServiceFixture
    {
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), $"bookshelf-identity-map-{Guid.NewGuid():N}.db");
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
CREATE TABLE BookshelfLegacyIdentityMap (
    Source TEXT NOT NULL,
    EntityType TEXT NOT NULL,
    SourceId INTEGER NOT NULL,
    TargetId INTEGER NOT NULL,
    MediaType INTEGER NULL
);
INSERT INTO BookshelfLegacyIdentityMap VALUES ('BookshelfNG', 'Book', 30, 30, 0);
INSERT INTO BookshelfLegacyIdentityMap VALUES ('BookshelfNG', 'Book', 30, 31, 1);
INSERT INTO BookshelfLegacyIdentityMap VALUES ('BookshelfNG', 'Book', 32, 32, 1);
INSERT INTO BookshelfLegacyIdentityMap VALUES ('OtherSource', 'Book', 30, 900, 0);";
            command.ExecuteNonQuery();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }

        private string ConnectionString => new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Pooling = false
        }.ConnectionString;

        [Test]
        public void should_resolve_a_split_source_book_to_both_or_the_requested_format()
        {
            var database = DispatchProxy.Create<IMainDatabase, MainDatabaseProxy>();
            ((MainDatabaseProxy)(object)database).ConnectionString = ConnectionString;
            var subject = new BookshelfLegacyIdentityMapService(database);

            var allFormats = subject.ResolveBookIds(new[] { 30, 99 });
            var ebooks = subject.ResolveBookIds(new[] { 30, 99 }, BookMediaType.Ebook);
            var audiobooks = subject.ResolveBookIds(new[] { 30 }, BookMediaType.Audiobook);

            Assert.Multiple(() =>
            {
                Assert.That(allFormats, Is.EqualTo(new[] { 30, 31, 99 }));
                Assert.That(ebooks, Is.EqualTo(new[] { 31, 99 }));
                Assert.That(audiobooks, Is.EqualTo(new[] { 30 }));
            });
        }

        [Test]
        public void should_return_only_requested_bookshelf_source_mappings()
        {
            var database = DispatchProxy.Create<IMainDatabase, MainDatabaseProxy>();
            ((MainDatabaseProxy)(object)database).ConnectionString = ConnectionString;
            var subject = new BookshelfLegacyIdentityMapService(database);

            var mappings = subject.GetMappings("Book", new[] { 30 });

            Assert.That(mappings.Select(mapping => (mapping.Source, mapping.SourceId, mapping.TargetId, mapping.MediaType)), Is.EqualTo(new[]
            {
                ("BookshelfNG", 30, 30, (int?)0),
                ("BookshelfNG", 30, 31, (int?)1)
            }));
        }

        private class MainDatabaseProxy : DispatchProxy
        {
            public string ConnectionString { get; set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                if (targetMethod?.Name == nameof(IDatabase.OpenConnection))
                {
                    var connection = new SqliteConnection(ConnectionString);
                    connection.Open();
                    return connection;
                }

                throw new NotImplementedException($"Test proxy does not implement {targetMethod?.Name}");
            }
        }
    }
}
