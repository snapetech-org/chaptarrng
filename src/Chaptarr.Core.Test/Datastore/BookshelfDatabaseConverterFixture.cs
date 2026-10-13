using System;
using System.IO;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using NzbDrone.Core.Datastore;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    public class BookshelfDatabaseConverterFixture
    {
        private string _directory;
        private string _sourcePath;
        private string _sourceConnectionString;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"bookshelf-converter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            _sourcePath = Path.Combine(_directory, "readarr.db");
            _sourceConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _sourcePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ConnectionString;

            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                CREATE TABLE AuthorMetadata (Id INTEGER PRIMARY KEY, ForeignAuthorId TEXT NOT NULL);
                CREATE TABLE Authors (
                    Id INTEGER PRIMARY KEY,
                    AuthorMetadataId INTEGER NOT NULL,
                    EbookPath TEXT,
                    AudiobookPath TEXT,
                    Path TEXT,
                    FOREIGN KEY (AuthorMetadataId) REFERENCES AuthorMetadata(Id));
                CREATE TABLE Books (Id INTEGER PRIMARY KEY, AuthorMetadataId INTEGER NOT NULL, ForeignBookId TEXT NOT NULL);
                CREATE TABLE Commands (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL);
                INSERT INTO AuthorMetadata VALUES (4, 'fixture-author');
                INSERT INTO Authors VALUES (8, 4, '/books/ebook', '/books/audio', '/books');
                INSERT INTO Books VALUES (12, 4, 'fixture-book');
                INSERT INTO Commands VALUES (16, 'stale command');";
            command.ExecuteNonQuery();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("wal"));
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [Test]
        public void should_promote_a_valid_staged_database_and_retain_a_source_backup()
        {
            var converter = new BookshelfDatabaseConverter();
            var backupPath = converter.Convert(
                _sourceConnectionString,
                CreateNativeTarget,
                (source, target, transaction) =>
                {
                    using var read = source.CreateCommand();
                    read.CommandText = "SELECT Id, Path FROM Authors WHERE Id = 8;";
                    using var reader = read.ExecuteReader();
                    Assert.That(reader.Read(), Is.True);
                    var authorId = reader.GetInt32(0);
                    var authorPath = reader.GetString(1);
                    reader.Close();

                    using var metadata = source.CreateCommand();
                    metadata.CommandText = "SELECT ForeignAuthorId FROM AuthorMetadata WHERE Id = 4;";
                    var foreignAuthorId = metadata.ExecuteScalar()?.ToString();

                    using var insert = target.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = @"
                        INSERT INTO Authors (Id, ForeignAuthorId, Name, Path, AudiobookRootFolderPath, EbookRootFolderPath)
                        VALUES (@id, @foreignAuthorId, 'Fixture Author', @path, @audio, @ebook);
                        INSERT INTO Books (Id, AuthorId, ForeignBookId)
                        VALUES (12, @id, 'fixture-book');";
                    insert.Parameters.AddWithValue("@id", authorId);
                    insert.Parameters.AddWithValue("@foreignAuthorId", foreignAuthorId);
                    insert.Parameters.AddWithValue("@path", authorPath);
                    insert.Parameters.AddWithValue("@audio", "/books/audio");
                    insert.Parameters.AddWithValue("@ebook", "/books/ebook");
                    insert.ExecuteNonQuery();

                    var report = new BookshelfDatabaseImportReport();
                    report.RecordTable("AuthorMetadata", 1, 0, 1, 0, 0, "merged into target Authors metadata");
                    report.RecordTable("Authors", 1, 0, 1, 0, 0, "converted to a managed target Author");
                    report.RecordTable("Books", 1, 0, 1, 0, 0, "mapped to target Books");
                    report.RecordTable("Commands", 1, 0, 0, 0, 1, "stale queued commands are reset; native startup recreates scheduled work");
                    return report;
                });

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(backupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(ReadJournalMode(_sourcePath), Is.EqualTo("delete"));
                Assert.That(File.Exists(_sourcePath + "-wal"), Is.False);
                Assert.That(File.Exists(_sourcePath + "-shm"), Is.False);
                Assert.That(ReadAuthorName(_sourcePath), Is.EqualTo("/books"));
                Assert.That(ReadBookAuthorId(_sourcePath), Is.EqualTo(8));
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadJournalMode(backupPath), Is.EqualTo("delete"));
                Assert.That(File.Exists(backupPath + "-wal"), Is.False);
                Assert.That(File.Exists(backupPath + "-shm"), Is.False);
                Assert.That(ReadBookId(backupPath), Is.EqualTo("fixture-book"));
            });
        }

        [Test]
        public void should_not_promote_when_a_source_table_is_missing_from_the_import_report()
        {
            var converter = new BookshelfDatabaseConverter();

            Assert.Throws<InvalidDataException>(() => converter.Convert(
                _sourceConnectionString,
                CreateNativeTarget,
                (source, target, transaction) =>
                {
                    var report = new BookshelfDatabaseImportReport();
                    report.RecordTable("Authors", 1, 0, 1, 0, 0, "converted to a managed target Author");
                    report.RecordTable("AuthorMetadata", 1, 0, 1, 0, 0, "merged into target Authors metadata");
                    return report;
                }));

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadBookId(_sourcePath), Is.EqualTo("fixture-book"));
                Assert.That(Directory.GetFiles(_directory, "*.chaptarr-stage-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-snapshot-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_not_promote_when_the_importer_leaves_source_rows_unaccounted_for()
        {
            var converter = new BookshelfDatabaseConverter();

            Assert.Throws<InvalidDataException>(() => converter.Convert(
                _sourceConnectionString,
                CreateNativeTarget,
                (source, target, transaction) =>
                {
                    var report = new BookshelfDatabaseImportReport();
                    report.RecordTable("Authors", 1, 0, 1, 0, 0, "converted to a managed target Author");
                    report.RecordTable("AuthorMetadata", 1, 0, 1, 0, 0, "merged into target Authors metadata");
                    report.RecordTable("Books", 1, 0, 0, 0, 0, "pending target mapping");
                    report.RecordTable("Commands", 1, 0, 0, 0, 1, "reset stale commands");
                    return report;
                }));

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadBookId(_sourcePath), Is.EqualTo("fixture-book"));
                Assert.That(Directory.GetFiles(_directory, "*.chaptarr-stage-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-snapshot-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_report_the_source_and_classified_counts_when_importer_source_count_is_wrong()
        {
            var converter = new BookshelfDatabaseConverter();

            var exception = Assert.Throws<InvalidDataException>(() => converter.Convert(
                _sourceConnectionString,
                CreateNativeTarget,
                (source, target, transaction) =>
                {
                    var report = new BookshelfDatabaseImportReport();
                    report.RecordTable("Authors", 1, 0, 1, 0, 0, "converted to a managed target Author");
                    report.RecordTable("AuthorMetadata", 1, 0, 1, 0, 0, "merged into target Authors metadata");
                    report.RecordTable("Books", 0, 0, 0, 0, 0, "reported as empty");
                    report.RecordTable("Commands", 1, 0, 0, 0, 1, "reset stale commands");
                    return report;
                }));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("reported source rows 0/1; classified rows 0/1"));
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadBookId(_sourcePath), Is.EqualTo("fixture-book"));
            });
        }

        [Test]
        public void should_leave_the_source_usable_when_import_fails()
        {
            var converter = new BookshelfDatabaseConverter();

            Assert.Throws<InvalidOperationException>(() => converter.Convert(
                _sourceConnectionString,
                CreateNativeTarget,
                (source, target, transaction) => throw new InvalidOperationException("synthetic import failure")));

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadBookId(_sourcePath), Is.EqualTo("fixture-book"));
                Assert.That(Directory.GetFiles(_directory, "*.chaptarr-stage-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-snapshot-*.db"), Is.Empty);
            });
        }

        private static void CreateNativeTarget(string connectionString)
        {
            using var target = new SqliteConnection(connectionString);
            target.Open();
            using var command = target.CreateCommand();
            command.CommandText = @"
                CREATE TABLE Authors (
                    Id INTEGER PRIMARY KEY,
                    ForeignAuthorId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Path TEXT,
                    AudiobookRootFolderPath TEXT,
                    EbookRootFolderPath TEXT);";
            command.ExecuteNonQuery();
            command.CommandText = "CREATE TABLE Books (Id INTEGER PRIMARY KEY, AuthorId INTEGER NOT NULL, ForeignBookId TEXT NOT NULL);";
            command.ExecuteNonQuery();
            command.CommandText = "PRAGMA journal_mode=WAL;";
            Assert.That(command.ExecuteScalar()?.ToString(), Is.EqualTo("wal"));
        }

        private static string ConnectionStringFor(string databasePath) => new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;

        private static string ReadBookId(string databasePath)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ForeignBookId FROM Books WHERE Id = 12;";
            return command.ExecuteScalar()?.ToString();
        }

        private static string ReadJournalMode(string databasePath)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode;";
            return command.ExecuteScalar()?.ToString();
        }

        private static string ReadAuthorName(string databasePath)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Path FROM Authors WHERE Id = 8;";
            return command.ExecuteScalar()?.ToString();
        }

        private static int ReadBookAuthorId(string databasePath)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT AuthorId FROM Books WHERE Id = 12;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }
}
