using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Conventions;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using Npgsql;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    [NonParallelizable]
    public class BookshelfPostgresDatabaseConverterFixture
    {
        private string _baseConnectionString;
        private string _sourceSchema;
        private string _mediaMapBasePath;
        private string _temporaryDirectory;
        private HashSet<string> _schemasBefore;
        private NpgsqlConnection _testLockConnection;
        private string _previousHardcoverMode;
        private string _previousHardcoverNativeMode;
        private readonly List<string> _knownBackupSchemas = new();

        [SetUp]
        public void SetUp()
        {
            _previousHardcoverMode = Environment.GetEnvironmentVariable("HARDCOVER");
            _previousHardcoverNativeMode = Environment.GetEnvironmentVariable("HARDCOVER_NATIVE");
            Environment.SetEnvironmentVariable("HARDCOVER", null);
            Environment.SetEnvironmentVariable("HARDCOVER_NATIVE", null);
            _knownBackupSchemas.Clear();
            _baseConnectionString = Environment.GetEnvironmentVariable("CHAPTARRNG_POSTGRES_TEST_CONNECTION");
            if (string.IsNullOrWhiteSpace(_baseConnectionString))
            {
                Assert.Ignore("Set CHAPTARRNG_POSTGRES_TEST_CONNECTION to run the BookshelfNG PostgreSQL conversion smoke test.");
            }

            _testLockConnection = new NpgsqlConnection(_baseConnectionString);
            _testLockConnection.Open();
            using (var lockCommand = _testLockConnection.CreateCommand())
            {
                lockCommand.CommandText = "SELECT pg_advisory_lock(hashtext(current_database()), hashtext('ChaptarrNG Bookshelf PostgreSQL converter fixture'));";
                lockCommand.ExecuteNonQuery();
            }

            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "chaptarr-bookshelf-postgres-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
            _mediaMapBasePath = Path.Combine(_temporaryDirectory, "readarr.db");
            _sourceSchema = "bookshelf_test_" + Guid.NewGuid().ToString("N").Substring(0, 12);
            _schemasBefore = GetSchemaNames(_baseConnectionString);
            CreateSchema(_baseConnectionString, _sourceSchema);
        }

        [TearDown]
        public void TearDown()
        {
            if (string.IsNullOrWhiteSpace(_baseConnectionString))
            {
                Environment.SetEnvironmentVariable("HARDCOVER", _previousHardcoverMode);
                Environment.SetEnvironmentVariable("HARDCOVER_NATIVE", _previousHardcoverNativeMode);
                return;
            }

            try
            {
                var generatedSchemas = _knownBackupSchemas
                    .Append(_sourceSchema)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                foreach (var schema in generatedSchemas)
                {
                    DropSchema(_baseConnectionString, schema);
                }

                if (_temporaryDirectory != null && Directory.Exists(_temporaryDirectory))
                {
                    Directory.Delete(_temporaryDirectory, recursive: true);
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("HARDCOVER", _previousHardcoverMode);
                Environment.SetEnvironmentVariable("HARDCOVER_NATIVE", _previousHardcoverNativeMode);
                if (_testLockConnection != null)
                {
                    try
                    {
                        using var unlockCommand = _testLockConnection.CreateCommand();
                        unlockCommand.CommandText = "SELECT pg_advisory_unlock(hashtext(current_database()), hashtext('ChaptarrNG Bookshelf PostgreSQL converter fixture'));";
                        unlockCommand.ExecuteNonQuery();
                    }
                    finally
                    {
                        _testLockConnection.Dispose();
                        _testLockConnection = null;
                    }
                }
            }
        }

        [Test]
        public void should_preview_convert_and_restore_a_current_bookshelf_postgres_schema()
        {
            var sourceConnectionString = BuildConnectionString(_baseConnectionString, _sourceSchema);
            MigrateBookshelfSourceToLatest(sourceConnectionString);
            SeedMatchingThreshold(sourceConnectionString);
            SeedCurrentBookshelfSourceWithLibrary(sourceConnectionString);

            var controller = new MigrationController(
                LogManager.GetLogger(nameof(BookshelfPostgresDatabaseConverterFixture)),
                null,
                new TestAppFolderInfo(_temporaryDirectory));

            Environment.SetEnvironmentVariable("HARDCOVER", "true");

            var preview = controller.MigrateBookshelf(
                sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.PostgreSQL,
                previewOnly: true);

            Assert.That(preview.PreviewOnly, Is.True);
            Assert.That(preview.BackupPath, Is.Null);
            Assert.That(preview.ImportReport.Tables["Books"].SourceRows, Is.EqualTo(1));
            Assert.That(preview.ImportReport.Tables["BookFiles"].SourceRows, Is.EqualTo(1));
            Assert.That(preview.ImportReport.Tables["Books"].TargetMapping, Does.Contain("Hardcover"));
            Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.BookshelfNG));
            Assert.That(GetSchemaNames(_baseConnectionString).Except(_schemasBefore, StringComparer.Ordinal).Any(schema => schema.StartsWith("chaptarr_stage_", StringComparison.Ordinal)), Is.False);

            controller.Migrate(
                sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.PostgreSQL);
            var bookshelfBackupSchema = GetSchemaNames(_baseConnectionString)
                .Except(_schemasBefore, StringComparer.Ordinal)
                .Single(schema => schema.StartsWith("bookshelfng_backup_", StringComparison.Ordinal));
            _knownBackupSchemas.Add(bookshelfBackupSchema);

            Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.Chaptarr));
            Assert.That(ReadConfigValue(sourceConnectionString, "bookmatchingstrictness"), Is.EqualTo("balanced"));
            Assert.That(ReadConfigValue(sourceConnectionString, "bookimportminimummatchpercent"), Is.EqualTo("70"));
            Assert.That(ReadConfigValue(sourceConnectionString, "HardcoverApiToken"), Is.EqualTo("pg-fixture-token"));
            Assert.That(ReadConfigValue(sourceConnectionString, "HardcoverAuth"), Is.EqualTo("Bearer pg-fixture-token"));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Authors\" WHERE \"Id\" = 20 AND \"HardcoverAuthorId\" = 'hc:10';"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Books\" WHERE \"Id\" = 30 AND \"HardcoverBookId\" = 'hc:30';"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Editions\" WHERE \"Id\" = 40 AND \"HardcoverEditionId\" = '40';"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Series\" WHERE \"Id\" = 70 AND \"HardcoverSeriesId\" = 'hc:70';"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Books\" WHERE \"Id\" = 30 AND \"MediaType\" = 0 AND \"AudiobookMonitored\" = TRUE;"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"Editions\" WHERE \"Id\" = 40 AND \"BookId\" = 30 AND \"Monitored\" = TRUE;"), Is.EqualTo(1));
            Assert.That(ReadScalarInt(sourceConnectionString, "SELECT COUNT(*) FROM \"BookFiles\" WHERE \"Id\" = 50 AND \"EditionId\" = 40 AND \"Path\" = '/library/Fixture Author/book.m4b';"), Is.EqualTo(1));
            Assert.That(ReadScalarLong(sourceConnectionString, "SELECT nextval(pg_get_serial_sequence('\"BookFiles\"', 'Id'));"), Is.GreaterThan(50));

            var bookshelfBackupConnectionString = BuildConnectionString(_baseConnectionString, bookshelfBackupSchema);
            Assert.That(DatabaseLineageGuard.Detect(bookshelfBackupConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.BookshelfNG));
            Assert.That(ReadConfigValue(bookshelfBackupConnectionString, "bookimportminimummatchpercent"), Is.EqualTo("70"));
            Assert.That(ReadConfigValue(bookshelfBackupConnectionString, "HardcoverAuth"), Is.EqualTo("Bearer pg-fixture-token"));
            WriteConfigValue(sourceConnectionString, "FixtureSetting", "after-migration");

            var restorePreview = controller.RestoreBookshelf(
                sourceConnectionString,
                DatabaseType.PostgreSQL,
                bookshelfBackupSchema,
                previewOnly: true);
            Assert.That(restorePreview.PreviewOnly, Is.True);
            Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.Chaptarr));
            Assert.That(ReadConfigValue(sourceConnectionString, "FixtureSetting"), Is.EqualTo("after-migration"));

            var restored = controller.RestoreBookshelf(
                sourceConnectionString,
                DatabaseType.PostgreSQL,
                bookshelfBackupSchema,
                previewOnly: false);
            _knownBackupSchemas.Add(restored.ChaptarrBackupPath);

            Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.BookshelfNG));
            Assert.That(ReadConfigValue(sourceConnectionString, "bookimportminimummatchpercent"), Is.EqualTo("70"));
            Assert.That(ReadConfigValue(sourceConnectionString, "HardcoverAuth"), Is.EqualTo("Bearer pg-fixture-token"));
            Assert.That(ReadConfigValue(sourceConnectionString, "FixtureSetting"), Is.EqualTo("preserved"));
            var chaptarrBackupConnectionString = BuildConnectionString(_baseConnectionString, restored.ChaptarrBackupPath);
            Assert.That(DatabaseLineageGuard.Detect(chaptarrBackupConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.Chaptarr));
            Assert.That(ReadConfigValue(chaptarrBackupConnectionString, "FixtureSetting"), Is.EqualTo("after-migration"));
        }

        [Test]
        public void should_reject_a_newer_postgres_source_without_changing_its_tables()
        {
            var sourceConnectionString = BuildConnectionString(_baseConnectionString, _sourceSchema);
            MigrateBookshelfSourceToLatest(sourceConnectionString);
            using (var connection = new NpgsqlConnection(sourceConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE \"VersionInfo\" SET \"Version\" = 159 WHERE \"Version\" = 158;";
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var controller = new MigrationController(
                LogManager.GetLogger(nameof(BookshelfPostgresDatabaseConverterFixture)),
                null,
                new TestAppFolderInfo(_temporaryDirectory));
            var exception = Assert.Throws<InvalidDataException>(() => controller.MigrateBookshelf(
                sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.PostgreSQL,
                previewOnly: false));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("schema version 159 is not supported"));
                Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadScalarInt(sourceConnectionString, "SELECT MAX(\"Version\") FROM \"VersionInfo\";"), Is.EqualTo(159));
                var newSchemas = GetSchemaNames(_baseConnectionString).Except(_schemasBefore, StringComparer.Ordinal).ToArray();
                Assert.That(newSchemas.Any(schema => schema.StartsWith("bookshelf_snapshot_", StringComparison.Ordinal)), Is.False);
                Assert.That(newSchemas.Any(schema => schema.StartsWith("chaptarr_stage_", StringComparison.Ordinal)), Is.False);
                Assert.That(newSchemas.Any(schema => schema.StartsWith("bookshelfng_backup_", StringComparison.Ordinal)), Is.False);
            });
        }

        [Test]
        public void should_reject_a_postgres_source_view_without_changing_its_tables()
        {
            var sourceConnectionString = BuildConnectionString(_baseConnectionString, _sourceSchema);
            MigrateBookshelfSourceToLatest(sourceConnectionString);
            using (var connection = new NpgsqlConnection(sourceConnectionString))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE VIEW \"BookshelfFixtureView\" AS SELECT 1 AS \"Value\";";
                command.ExecuteNonQuery();
            }

            var controller = new MigrationController(
                LogManager.GetLogger(nameof(BookshelfPostgresDatabaseConverterFixture)),
                null,
                new TestAppFolderInfo(_temporaryDirectory));
            var exception = Assert.Throws<InvalidDataException>(() => controller.MigrateBookshelf(
                sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.PostgreSQL,
                previewOnly: true));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("view"));
                Assert.That(DatabaseLineageGuard.Detect(sourceConnectionString, DatabaseType.PostgreSQL), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(ReadScalarInt(sourceConnectionString, "SELECT MAX(\"Version\") FROM \"VersionInfo\";"), Is.EqualTo(158));
                var newSchemas = GetSchemaNames(_baseConnectionString).Except(_schemasBefore, StringComparer.Ordinal).ToArray();
                Assert.That(newSchemas.Any(schema => schema.StartsWith("bookshelf_snapshot_", StringComparison.Ordinal)), Is.False);
                Assert.That(newSchemas.Any(schema => schema.StartsWith("chaptarr_stage_", StringComparison.Ordinal)), Is.False);
                Assert.That(newSchemas.Any(schema => schema.StartsWith("bookshelfng_backup_", StringComparison.Ordinal)), Is.False);
            });
        }

        private void MigrateBookshelfSourceToLatest(string connectionString)
        {
            var migrationServices = new ServiceCollection()
                .AddLogging(builder => builder.AddNLog())
                .AddFluentMigratorCore()
                .Configure<RunnerOptions>(options => options.IncludeUntaggedMaintenances = true)
                .ConfigureRunner(builder => builder
                    .AddPostgres()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(BookshelfDatabaseImporter).Assembly).For.All())
                .Configure<TypeFilterOptions>(options => options.Namespace = "NzbDrone.Core.Datastore.BookshelfMigration")
                .Configure<ProcessorOptions>(options =>
                {
                    options.PreviewOnly = false;
                    options.Timeout = TimeSpan.FromMinutes(5);
                })
                .Configure<SelectingProcessorAccessorOptions>(options => options.ProcessorId = "PostgreSQL")
                .Configure<SelectingGeneratorAccessorOptions>(options => options.GeneratorId = "PostgreSQL");
            migrationServices.AddSingleton<IConventionSet>(new DefaultConventionSet(_sourceSchema, null));
            using var serviceProvider = migrationServices.BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();
            MigrationContext.Current = new MigrationContext(MigrationType.Main);
            try
            {
                scope.ServiceProvider.GetRequiredService<IMigrationRunner>().MigrateUp();
            }
            finally
            {
                MigrationContext.Current = null;
            }
        }

        private static void SeedMatchingThreshold(string connectionString)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Config (""Key"", ""Value"")
                VALUES
                    ('BookImportMinimumMatchPercent', '70'),
                    ('HardcoverAuth', 'Bearer pg-fixture-token')
                ON CONFLICT (""Key"") DO UPDATE SET ""Value"" = EXCLUDED.""Value"";";
            command.ExecuteNonQuery();
        }

        private static void SeedCurrentBookshelfSourceWithLibrary(string connectionString)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO ""QualityProfiles"" (""Id"", ""Name"", ""Cutoff"", ""Items"", ""UpgradeAllowed"")
                VALUES (1, 'Audio default', 1, '[]', TRUE);
                INSERT INTO ""MetadataProfiles"" (""Id"", ""Name"", ""MinPopularity"", ""SkipMissingDate"", ""SkipMissingIsbn"", ""SkipPartsAndSets"", ""SkipSeriesSecondary"", ""AllowedLanguages"", ""PreferredEditionTerms"", ""PreferredSeries"")
                VALUES (1, 'Audio metadata', 0, FALSE, FALSE, FALSE, FALSE, '[]', '3|audible', 'The Fixture Series');
                INSERT INTO ""RootFolders"" (""Id"", ""Path"", ""Name"", ""DefaultMetadataProfileId"", ""DefaultQualityProfileId"", ""DefaultMonitorOption"", ""DefaultTags"", ""IsCalibreLibrary"", ""CalibreSettings"", ""DefaultNewItemMonitorOption"")
                VALUES (1, '/library', 'Library', 1, 1, 0, '[]', FALSE, NULL, 2);
                INSERT INTO ""Config"" (""Id"", ""Key"", ""Value"") VALUES (2, 'FixtureSetting', 'preserved');
                INSERT INTO ""AuthorMetadata"" (""Id"", ""ForeignAuthorId"", ""TitleSlug"", ""Name"", ""Overview"", ""Status"", ""Images"", ""Aliases"", ""SortName"", ""NameLastFirst"", ""SortNameLastFirst"")
                VALUES (10, '10', 'fixture-author', 'Fixture Author', 'Fixture overview', 0, '[]', '[]', 'Fixture Author', 'Author, Fixture', 'Author, Fixture');
                INSERT INTO ""Authors"" (""Id"", ""CleanName"", ""Path"", ""Monitored"", ""QualityProfileId"", ""Tags"", ""Added"", ""MetadataProfileId"", ""AuthorMetadataId"", ""MonitorNewItems"", ""EbookPath"", ""AudiobookPath"", ""EbookQualityProfileId"", ""AudiobookQualityProfileId"", ""EbookMetadataProfileId"", ""AudiobookMetadataProfileId"")
                VALUES (20, 'fixtureauthor', '/library/Fixture Author', TRUE, 1, '[]', '2026-01-01 00:00:00+00', 1, 10, 2, '/library/Fixture Author/Ebooks', '/library/Fixture Author/Audiobooks', NULL, 1, NULL, 1);
                INSERT INTO ""Books"" (""Id"", ""AuthorMetadataId"", ""ForeignBookId"", ""TitleSlug"", ""Title"", ""Links"", ""Genres"", ""Ratings"", ""CleanTitle"", ""Monitored"", ""AnyEditionOk"", ""Added"", ""RelatedBooks"", ""LastSearchTime"")
                VALUES (30, 10, '30', 'fixture-book', 'Fixture Book', '[]', '[]', '{}', 'fixture book', TRUE, FALSE, '2026-01-02 00:00:00+00', '[]', NULL);
                INSERT INTO ""Editions"" (""Id"", ""BookId"", ""ForeignEditionId"", ""Isbn13"", ""Asin"", ""Title"", ""TitleSlug"", ""Images"", ""Links"", ""IsEbook"", ""Monitored"", ""ManualAdd"", ""Narrator"")
                VALUES (40, 30, '40', NULL, NULL, 'Fixture Audiobook', 'audio-40', '[]', '[]', FALSE, TRUE, FALSE, 'A Narrator');
                INSERT INTO ""Series"" (""Id"", ""ForeignSeriesId"", ""Title"", ""Numbered"", ""WorkCount"", ""PrimaryWorkCount"")
                VALUES (70, '70', 'Fixture Series', TRUE, 1, 1);
                INSERT INTO ""BookFiles"" (""Id"", ""EditionId"", ""CalibreId"", ""Quality"", ""Size"", ""DateAdded"", ""MediaInfo"", ""Modified"", ""Path"", ""Part"")
                VALUES (50, 40, 0, '{}', 100, '2026-01-02 00:00:00+00', '{}', '2026-01-02 00:00:00+00', '/library/Fixture Author/book.m4b', 0);";
            command.ExecuteNonQuery();
        }

        private static string ReadConfigValue(string connectionString, string key)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT \"Value\" FROM Config WHERE \"Key\" = @key;";
            command.Parameters.AddWithValue("@key", key);
            return command.ExecuteScalar()?.ToString();
        }

        private static void WriteConfigValue(string connectionString, string key, string value)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE \"Config\" SET \"Value\" = @value WHERE \"Key\" = @key;";
            command.Parameters.AddWithValue("@value", value);
            command.Parameters.AddWithValue("@key", key);
            Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
        }

        private static int ReadScalarInt(string connectionString, string sql)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static long ReadScalarLong(string connectionString, string sql)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string BuildConnectionString(string connectionString, string searchPath)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = searchPath,
                Pooling = false,
                Enlist = false
            };
            return builder.ConnectionString;
        }

        private static HashSet<string> GetSchemaNames(string connectionString)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT schema_name FROM information_schema.schemata;";
            using var reader = command.ExecuteReader();
            var result = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }

        private static void CreateSchema(string connectionString, string schema)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE SCHEMA \"" + schema.Replace("\"", "\"\"", StringComparison.Ordinal) + "\";";
            command.ExecuteNonQuery();
        }

        private static void DropSchema(string connectionString, string schema)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP SCHEMA IF EXISTS \"" + schema.Replace("\"", "\"\"", StringComparison.Ordinal) + "\" CASCADE;";
            command.ExecuteNonQuery();
        }

        private sealed class TestAppFolderInfo : IAppFolderInfo
        {
            public TestAppFolderInfo(string appDataFolder)
            {
                AppDataFolder = appDataFolder;
            }

            public string AppDataFolder { get; }
            public string TempFolder => AppDataFolder;
            public string StartUpFolder => AppDataFolder;
        }
    }
}
