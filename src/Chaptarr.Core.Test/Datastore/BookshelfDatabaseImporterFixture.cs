using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentMigrator.Runner.Generators;
using FluentMigrator.Runner;
using FluentMigrator.Runner.Initialization;
using FluentMigrator.Runner.Processors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using NLog;
using NLog.Extensions.Logging;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Indexers.DirectDownload;
using NzbDrone.Core.Indexers.MyAnonaMouse;
using NzbDrone.Core.MetadataSource.BookInfo;
using NzbDrone.Core.Notifications.BookLore;

namespace Chaptarr.Core.Test.Datastore
{
    [TestFixture]
    [NonParallelizable]
    public class BookshelfDatabaseImporterFixture
    {
        private string _directory;
        private string _sourcePath;
        private string _sourceConnectionString;
        private string _previousHardcoverMode;
        private string _previousHardcoverNativeMode;

        [SetUp]
        public void SetUp()
        {
            _previousHardcoverMode = Environment.GetEnvironmentVariable("HARDCOVER");
            _previousHardcoverNativeMode = Environment.GetEnvironmentVariable("HARDCOVER_NATIVE");
            Environment.SetEnvironmentVariable("HARDCOVER", null);
            Environment.SetEnvironmentVariable("HARDCOVER_NATIVE", null);

            _directory = Path.Combine(Path.GetTempPath(), $"bookshelf-importer-{Guid.NewGuid().ToString("N").Substring(0, 8)}");
            Directory.CreateDirectory(_directory);
            _sourcePath = Path.Combine(_directory, "readarr.db");
            _sourceConnectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _sourcePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ConnectionString;

            CreateSourceDatabase();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
            Environment.SetEnvironmentVariable("HARDCOVER", _previousHardcoverMode);
            Environment.SetEnvironmentVariable("HARDCOVER_NATIVE", _previousHardcoverNativeMode);
        }

        [Test]
        public void should_preserve_bare_numeric_provider_ids_as_hardcover_ids_when_native_mode_is_enabled()
        {
            SetBareNumericProviderIds();
            Environment.SetEnvironmentVariable("HARDCOVER", "true");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverAuthorId FROM Authors WHERE Id = 20;"), Is.EqualTo("hc:10"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverBookId FROM Books WHERE Id = 30;"), Is.EqualTo("hc:30"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT ForeignEditionId FROM Editions WHERE Id = 40;"), Is.EqualTo("hc:40"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverEditionId FROM Editions WHERE Id = 40;"), Is.EqualTo("40"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverSeriesId FROM Series WHERE Id = 70;"), Is.EqualTo("hc:70"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverEnabled';"), Is.EqualTo("true"),
                    "native BookshelfNG Hardcover mode must remain enabled after conversion");
            });
        }

        [Test]
        public void should_keep_bare_numeric_provider_ids_as_goodreads_ids_when_hardcover_mode_is_disabled()
        {
            SetBareNumericProviderIds();
            Environment.SetEnvironmentVariable("HARDCOVER", "false");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT GoodreadsAuthorId FROM Authors WHERE Id = 20;"), Is.EqualTo("gr:10"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT GoodreadsWorkId FROM Books WHERE Id = 30;"), Is.EqualTo("gr:30"));
                Assert.That(QueryScalar<long>(_sourcePath, "SELECT GoodreadsEditionId FROM Editions WHERE Id = 40;"), Is.EqualTo(40));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT GoodreadsSeriesId FROM Series WHERE Id = 70;"), Is.EqualTo("gr:70"));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Config WHERE Key = 'HardcoverEnabled' AND Value = 'true';"), Is.Zero,
                    "Goodreads mode must not be changed to native Hardcover during conversion");
            });
        }

        [Test]
        public void should_report_unmapped_provider_settings_in_preview_and_refuse_to_apply_them()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Indexers (Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId)
                    VALUES (123, 'Plugin fixture', 'FixturePluginIndexer', '{}', 'FixturePluginSettings', 1, 1, 1, 25, '[]', 0);
                    INSERT INTO Indexers (Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId)
                    VALUES (130, 'Legacy Torznab fixture', 'Torznab', '{""baseUrl"":""https://indexer.example"",""apiKey"":""fixture-indexer-key"",""legacyIndexerSecret"":""fixture-indexer-secret""}', 'TorznabSettings', 1, 1, 1, 25, '[]', 0);
                    INSERT INTO DownloadClients (Id, Enable, Name, Implementation, Settings, ConfigContract)
                    VALUES (124, 1, 'Plugin download fixture', 'FixturePluginDownload', '{}', 'FixturePluginDownloadSettings');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (125, 'Legacy Hardcover fixture', 'HardcoverImport', '{}', 'HardcoverImportSettings', 0, '/library', 0, 1, 1, '[]');
                    INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                    VALUES (126, 'Legacy Goodreads owned notification', 1, '{}', 'GoodreadsOwnedBooks', 'GoodreadsOwnedBooksNotificationSettings', 1, '[]', 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0);
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (127, 'Legacy Goodreads owned list', 'GoodreadsOwnedBooks', '{}', 'GoodreadsOwnedBooksImportListSettings', 0, '/library', 0, 1, 1, '[]');
                    INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                    VALUES (128, 'Legacy Goodreads bookshelf notification', 1, '{}', 'GoodreadsBookshelf', 'GoodreadsBookshelfNotificationSettings', 1, '[]', 1, 1, 1, 0, 1, 1, 1, 0, 0, 0, 0, 1, 0);
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (129, 'Legacy Goodreads shelf list', 'GoodreadsBookshelf', '{""userId"":""12345678"",""bookshelfIds"":[""to-read""],""accessToken"":""fixture-goodreads-access-secret"",""accessTokenSecret"":""fixture-goodreads-secret"",""requestTokenSecret"":""fixture-request-secret"",""userName"":""fixture-user"",""signIn"":""startOAuth"",""signingUrl"":""https://auth.servarr.com/v1/goodreads/sign"",""oauthUrl"":""https://www.goodreads.com/oauth/authorize"",""oauthRequestTokenUrl"":""https://www.goodreads.com/oauth/request_token"",""oauthAccessTokenUrl"":""https://www.goodreads.com/oauth/access_token""}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (131, 'Legacy public Goodreads shelf list', 'GoodreadsBookshelf', '{""userId"":""12345678"",""bookshelfIds"":[""to-read""],""accessToken"":"""",""accessTokenSecret"":"""",""requestTokenSecret"":"""",""userName"":"""",""signIn"":""startOAuth"",""signingUrl"":""https://auth.servarr.com/v1/goodreads/sign"",""oauthUrl"":""https://www.goodreads.com/oauth/authorize"",""oauthRequestTokenUrl"":""https://www.goodreads.com/oauth/request_token"",""oauthAccessTokenUrl"":""https://www.goodreads.com/oauth/access_token""}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (132, 'Legacy Goodreads partial OAuth shelf list', 'GoodreadsBookshelf', '{""userId"":""12345678"",""bookshelfIds"":[""read""],""accessToken"":""fixture-partial-goodreads-token""}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (133, 'Legacy Goodreads shelf without an account', 'GoodreadsBookshelf', '{""bookshelfIds"":[""read""]}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO Metadata (Id, Enable, Name, Implementation, Settings, ConfigContract)
                    VALUES (134, 1, 'Legacy OPF sidecar', 'AudiobookshelfMetadata', '{}', 'NullConfig');
                    INSERT INTO Metadata (Id, Enable, Name, Implementation, Settings, ConfigContract)
                    VALUES (135, 1, 'Unmapped metadata plugin', 'FixturePluginMetadata', '{""apiKey"":""fixture-metadata-secret""}', 'FixturePluginMetadataSettings');
                    INSERT INTO MetadataFiles (Id, AuthorId, Consumer, Type, RelativePath, LastUpdated, BookId, BookFileId, Extension)
                    VALUES (66, 20, 'FixturePluginMetadata', 0, 'plugin.sidecar', '2026-01-05', 30, NULL, 'sidecar');";
                command.ExecuteNonQuery();
            }

            var sourceBefore = File.ReadAllBytes(_sourcePath);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);
            var preview = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(preview.ImportReport.BlockingIssues, Has.Count.EqualTo(10));
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("FixturePluginIndexer/FixturePluginSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue =>
                    issue.Contains("Torznab/TorznabSettings", StringComparison.Ordinal) &&
                    issue.Contains("legacyIndexerSecret", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("fixture-indexer-secret", StringComparison.Ordinal)), Is.False,
                    "provider settings and credentials must not be included in the report");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("FixturePluginDownload/FixturePluginDownloadSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue =>
                    issue.Contains("AudiobookshelfMetadata/NullConfig", StringComparison.Ordinal)), Is.False,
                    "the legacy OPF metadata provider is mapped by its exact implementation and contract");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue =>
                    issue.Contains("FixturePluginMetadata/FixturePluginMetadataSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue =>
                    issue.Contains("FixturePluginMetadata", StringComparison.Ordinal) &&
                    issue.Contains("metadata consumer", StringComparison.OrdinalIgnoreCase)), Is.True,
                    "unmapped sidecar consumers must block apply");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("fixture-metadata-secret", StringComparison.Ordinal)), Is.False,
                    "metadata provider settings and credentials must not be included in the report");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("HardcoverImport/HardcoverImportSettings", StringComparison.Ordinal)), Is.False,
                    "the migrated Hardcover public-list provider is available in Chaptarr");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("GoodreadsOwnedBooks/GoodreadsOwnedBooksNotificationSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("GoodreadsBookshelf/GoodreadsBookshelfNotificationSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("GoodreadsOwnedBooks/GoodreadsOwnedBooksImportListSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue =>
                    issue.Contains("GoodreadsBookshelf/GoodreadsBookshelfImportListSettings", StringComparison.Ordinal)), Is.True);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("row 129:", StringComparison.Ordinal)), Is.False,
                    "complete legacy OAuth settings are supported by the migration candidate");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("row 132:", StringComparison.Ordinal) &&
                    issue.Contains("accessTokenSecret", StringComparison.Ordinal)), Is.True,
                    "partial OAuth credentials must block migration without exposing their values");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("row 133:", StringComparison.Ordinal) &&
                    issue.Contains("userId or OAuth credentials", StringComparison.Ordinal)), Is.True,
                    "public shelves without an account ID or complete OAuth credentials must block migration");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("row 131:", StringComparison.Ordinal)), Is.False,
                    "empty OAuth fields and fixed Goodreads runtime settings do not block public-shelf migration");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("fixture-goodreads-access-secret", StringComparison.Ordinal)), Is.False,
                    "provider settings and credentials must not be included in the report");
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("fixture-partial-goodreads-token", StringComparison.Ordinal)), Is.False,
                    "provider settings and credentials must not be included in the report");
                Assert.That(preview.ImportReport.Tables["DownloadClients"].TargetMapping, Does.Contain("block apply"));
                Assert.That(preview.ImportReport.Tables["ImportLists"].TargetMapping, Does.Contain("block apply"));
                Assert.That(preview.ImportReport.Tables["Notifications"].TargetMapping, Does.Contain("block apply"));
                Assert.That(preview.ImportReport.Tables["Indexers"].TargetMapping, Does.Contain("block apply"));
                Assert.That(preview.ImportReport.Tables["Metadata"].TargetMapping, Does.Contain("block apply"));
                Assert.That(preview.ImportReport.Tables["MetadataFiles"].TargetMapping, Does.Contain("block apply"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBefore), "preview must leave source bytes unchanged");
            });

            var exception = Assert.Throws<InvalidDataException>(() => migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("cannot be applied"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBefore), "a blocked apply must leave source bytes unchanged");
            });
        }

        [Test]
        public void should_preserve_goodreads_oauth_provider_settings_during_bookshelf_conversion()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = """
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (201, 'Legacy Goodreads OAuth shelf', 'GoodreadsBookshelf', '{"userId":"12345678","bookshelfIds":["to-read"],"accessToken":"fixture-access-token","accessTokenSecret":"fixture-access-secret","requestTokenSecret":"fixture-request-secret","userName":"fixture-user","signIn":"startOAuth","signingUrl":"https://auth.servarr.com/v1/goodreads/sign","oauthUrl":"https://www.goodreads.com/oauth/authorize","oauthRequestTokenUrl":"https://www.goodreads.com/oauth/request_token","oauthAccessTokenUrl":"https://www.goodreads.com/oauth/access_token"}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (202, 'Legacy Goodreads public shelf', 'GoodreadsBookshelf', '{"userId":"12345678","bookshelfIds":["to-read"],"accessToken":"","accessTokenSecret":"","requestTokenSecret":"","userName":"","signIn":"startOAuth","signingUrl":"https://auth.servarr.com/v1/goodreads/sign","oauthUrl":"https://www.goodreads.com/oauth/authorize","oauthRequestTokenUrl":"https://www.goodreads.com/oauth/request_token","oauthAccessTokenUrl":"https://www.goodreads.com/oauth/access_token"}', 'GoodreadsBookshelfImportListSettings', 1, '/library', 1, 1, 1, '[]');
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (203, 'Legacy Goodreads owned list', 'GoodreadsOwnedBooks', '{"userId":"12345678","accessToken":"fixture-owned-access-token","accessTokenSecret":"fixture-owned-access-secret","requestTokenSecret":"fixture-owned-request-secret","userName":"fixture-owned-user","signIn":"startOAuth"}', 'GoodreadsOwnedBooksImportListSettings', 0, '/library', 0, 1, 1, '[]');
                    INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                    VALUES (204, 'Legacy Goodreads bookshelf notification', 0, '{"accessToken":"fixture-notify-access-token","accessTokenSecret":"fixture-notify-access-secret","requestTokenSecret":"fixture-notify-request-secret","userId":"12345678","userName":"fixture-notify-user","addIds":["read"],"removeIds":["to-read"],"signIn":"startOAuth"}', 'GoodreadsBookshelf', 'GoodreadsBookshelfNotificationSettings', 0, '[]', 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                    VALUES (205, 'Legacy Goodreads owned notification', 0, '{"accessToken":"fixture-owned-notify-token","accessTokenSecret":"fixture-owned-notify-secret","requestTokenSecret":"fixture-owned-notify-request","userId":"12345678","userName":"fixture-owned-notify-user","condition":10,"description":"","location":"","signIn":"startOAuth"}', 'GoodreadsOwnedBooks', 'GoodreadsOwnedBooksNotificationSettings', 0, '[]', 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                    """;
                command.ExecuteNonQuery();
            }

            var sourceBeforePreview = File.ReadAllBytes(_sourcePath);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);
            var preview = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(preview.ImportReport.BlockingIssues, Is.Empty);
                Assert.That(preview.ImportReport.BlockingIssues.Any(issue => issue.Contains("fixture-access-secret", StringComparison.Ordinal)), Is.False);
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBeforePreview));
            });

            migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessToken') FROM ImportLists WHERE Id = 201;"), Is.EqualTo("fixture-access-token"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessTokenSecret') FROM ImportLists WHERE Id = 201;"), Is.EqualTo("fixture-access-secret"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessTokenSecret') FROM ImportLists WHERE Id = 203;"), Is.EqualTo("fixture-owned-access-secret"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessTokenSecret') FROM Notifications WHERE Id = 204;"), Is.EqualTo("fixture-notify-access-secret"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessTokenSecret') FROM Notifications WHERE Id = 205;"), Is.EqualTo("fixture-owned-notify-secret"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT json_extract(Settings, '$.accessTokenSecret') FROM ImportLists WHERE Id = 202;"), Is.EqualTo(string.Empty));
            });
        }

        [Test]
        public void should_preserve_hardcover_public_list_provider_settings()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO ImportLists (Id, Name, Implementation, Settings, ConfigContract, EnableAutomaticAdd, RootFolderPath, ShouldMonitor, ProfileId, MetadataProfileId, Tags)
                    VALUES (111, 'Hardcover public list', 'HardcoverImport', '{""apiKey"":""fixture-token"",""user"":""fixture-user"",""listIds"":[""fixture-list""]}', 'HardcoverImportSettings', 1, '/library', 1, 1, 1, '[]');";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Implementation FROM ImportLists WHERE Id = 111;"), Is.EqualTo("HardcoverImport"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM ImportLists WHERE Id = 111;"), Is.EqualTo("HardcoverImportSettings"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Settings FROM ImportLists WHERE Id = 111;"), Does.Contain("fixture-token"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Settings FROM ImportLists WHERE Id = 111;"), Does.Contain("fixture-list"));
            });
        }

        [Test]
        public void should_fail_closed_when_bare_numeric_provider_ids_have_no_explicit_source_mode()
        {
            SetBareNumericProviderIds();
            var sourceBefore = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() =>
                new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("primary catalog mode is not explicit"));
                Assert.That(exception.Message, Does.Contain("HARDCOVER=true"));
                Assert.That(exception.Message, Does.Contain("HARDCOVER=false"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBefore));
            });
        }

        [Test]
        public void should_replace_target_naming_defaults_when_bookshelf_has_saved_settings()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO NamingConfig (Id, ReplaceIllegalCharacters, AuthorFolderFormat, RenameBooks, StandardBookFormat, ColonReplacementFormat)
                    VALUES (5, 0, '{Author NameLastFirst}', 1, '{Book Title} custom', 4);";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM NamingConfig;"), Is.EqualTo(1),
                    "saved BookshelfNG settings must replace the seeded Chaptarr defaults");
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MultiEpisodeStyle FROM NamingConfig WHERE Id = 5;"), Is.Zero);
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT ReplaceIllegalCharacters FROM NamingConfig WHERE Id = 5;"), Is.Zero);
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT RenameBooks FROM NamingConfig WHERE Id = 5;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT AuthorFolderFormat FROM NamingConfig WHERE Id = 5;"), Is.EqualTo("{Author NameLastFirst}"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT StandardBookFormat FROM NamingConfig WHERE Id = 5;"), Is.EqualTo("{Book Title} custom"));
            });
        }

        [Test]
        public void should_convert_a_populated_v158_bookshelf_database_and_preserve_media_relationships()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(backupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;").Length, Is.Zero);
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM NamingConfig WHERE MultiEpisodeStyle = 0 AND LENGTH(StandardBookFormat) > 0 AND LENGTH(AuthorFolderFormat) > 0;"), Is.EqualTo(1),
                    "an empty BookshelfNG naming table must retain Chaptarr's migrated defaults");
                Assert.That(QueryScalar<string>(backupPath, "SELECT ForeignBookId FROM Books WHERE Id = 30;"), Is.EqualTo("hardcover:book-30"));
            });

            Assert.That(QueryScalar<string>(_sourcePath, "SELECT HardcoverAuthorId FROM Authors WHERE Id = 20;"), Is.EqualTo("hc:author-10"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT AudiobookQualityProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EbookQualityProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(4));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AudiobookPath FROM Authors WHERE Id = 20;"), Is.EqualTo("/library/Fixture Author/Audiobooks"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT EbookPath FROM Authors WHERE Id = 20;"), Is.EqualTo("/library/Fixture Author/Ebooks"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT AudiobookMetadataProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EbookMetadataProfileId FROM Authors WHERE Id = 20;"), Is.EqualTo(2));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT ProfileType FROM QualityProfiles WHERE Id = 1;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT ProfileType FROM QualityProfiles WHERE Id = 3;"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Name FROM QualityProfiles WHERE Id = 4;"), Is.EqualTo("Ebook override"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT Cutoff FROM QualityProfiles WHERE Id = 4;"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AudiobookSettings FROM RootFolders WHERE Id = 1;"), Does.Contain("\"QualityProfileId\":1"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT EbookSettings FROM RootFolders WHERE Id = 1;"), Does.Contain("\"QualityProfileId\":3"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredEditionTerms FROM MetadataProfiles WHERE Id = 1;"), Is.EqualTo("3|audible"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredSeries FROM MetadataProfiles WHERE Id = 1;"), Is.EqualTo("The Fixture Series"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredEditionTerms FROM MetadataProfiles WHERE Id = 2;"), Is.EqualTo("2|epub"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT PreferredSeries FROM MetadataProfiles WHERE Id = 2;"), Is.EqualTo("Fixture Ebooks"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("preserved"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT QualityProfileId FROM ImportLists WHERE Id = 110;"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM ImportLists WHERE Id = 110;"), Is.EqualTo("GoodreadsBookshelfImportListSettings"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Settings FROM ImportLists WHERE Id = 110;"), Does.Contain("12345678"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM Notifications WHERE Id = 111;"), Is.EqualTo(nameof(BookLoreSettings)));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM DownloadClients WHERE Id = 109;"), Is.EqualTo(nameof(QBittorrentSettings)));
            var qBittorrentSettings = NzbDrone.Common.Serializer.STJson.Deserialize<QBittorrentSettings>(
                QueryScalar<string>(_sourcePath, "SELECT Settings FROM DownloadClients WHERE Id = 109;"));
            Assert.That(qBittorrentSettings.AudiobookCategory, Is.EqualTo("legacy-audio"));
            Assert.That(qBittorrentSettings.EbookCategory, Is.EqualTo("legacy-ebooks"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Indexers WHERE Id = 120 AND EnableRss = 1 AND SupportsRss = 0 AND SupportsSearch = 0;"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM Indexers WHERE Id = 120;"), Is.EqualTo(nameof(MyAnonaMouseSettings)));
            var mamSettings = NzbDrone.Common.Serializer.STJson.Deserialize<MyAnonaMouseSettings>(
                QueryScalar<string>(_sourcePath, "SELECT Settings FROM Indexers WHERE Id = 120;"));
            Assert.Multiple(() =>
            {
                Assert.That(mamSettings.MamId, Is.EqualTo("fixture-token"));
                Assert.That(mamSettings.SearchType, Is.EqualTo((int)MyAnonaMouseSearchType.Freeleech));
                Assert.That(mamSettings.SearchInDescription, Is.True);
                Assert.That(mamSettings.SearchInSeries, Is.False);
                Assert.That(mamSettings.SearchInFilenames, Is.True);
                Assert.That(mamSettings.SearchLanguages, Is.EqualTo(new[] { 4, 1 }));
                Assert.That(mamSettings.UseFreeleechWedge, Is.EqualTo((int)MyAnonaMouseFreeleechWedgeAction.Never),
                    "the unsafe legacy Required value is conservatively mapped to Never");
            });
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM Indexers WHERE Id = 121;"), Is.EqualTo(nameof(DirectDownloadSettings)));
            var directDownloadSettings = NzbDrone.Common.Serializer.STJson.Deserialize<DirectDownloadSettings>(
                QueryScalar<string>(_sourcePath, "SELECT Settings FROM Indexers WHERE Id = 121;"));
            Assert.That(directDownloadSettings.Urls, Is.EqualTo("https://primary.example\nhttps://secondary.example"));
            Assert.That(directDownloadSettings.ApiKey, Is.EqualTo("fixture-key"));
            Assert.That(directDownloadSettings.EnableSlowFallback, Is.True);
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT ConfigContract FROM Indexers WHERE Id = 122;"), Is.EqualTo("NewznabSettings"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT OnBookRetag FROM Notifications WHERE Id = 111;"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Password FROM Users WHERE Id = 112;"), Is.EqualTo("legacy-sha256-hash"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Users WHERE Id = 112 AND Salt IS NULL AND Iterations IS NULL;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE AuthorId = 20;"), Is.EqualTo(2));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 1 AND ForeignEditionId = 'hc:audio-40';"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND EbookMonitored = 0 AND ForeignEditionId IS NULL;"), Is.EqualTo(1));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND HardcoverEditionId = 'audio-40';"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT NarratorNames FROM Editions WHERE Id = 40;"), Is.EqualTo("[\"A Narrator\"]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Images FROM Editions WHERE Id = 40;"), Is.EqualTo("[{\"Url\":\"https://covers.example/audio.jpg\",\"CoverType\":6}]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Links FROM Editions WHERE Id = 40;"), Is.EqualTo("[{\"Url\":\"https://catalog.example/audio-40\",\"Name\":\"Catalog\"}]"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 41 AND BookId = 31 AND OpenLibraryEditionId = 'ol:OL123M';"), Is.EqualTo(1));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT Links FROM Editions WHERE Id = 41;"), Is.EqualTo("[]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT MediaType FROM BookFiles WHERE Id = 51;"), Is.EqualTo("ebook"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookFileId FROM ExtraFiles WHERE Id = 60;"), Is.EqualTo(999));
            Assert.That(QueryScalar<long>(_sourcePath, "SELECT seq FROM sqlite_sequence WHERE name = 'BookFiles';"), Is.GreaterThanOrEqualTo(999));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM MetadataFiles WHERE Id = 61;"), Is.EqualTo(31));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM History WHERE Id = 93;"), Is.EqualTo(31));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT EditionId FROM History WHERE Id = 93;"), Is.EqualTo(41));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM SeriesBookLink WHERE SeriesId = 70 AND BookId IN (30, 31);"), Is.EqualTo(2));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT AuthorProviderIds FROM Blocklist WHERE Id = 92;"), Is.EqualTo("[\"hc:author-10\"]"));
            Assert.That(QueryScalar<string>(_sourcePath, "SELECT BookProviderIds FROM Blocklist WHERE Id = 92;"), Is.EqualTo("[\"hc:book-30\"]"));
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT BookId FROM DownloadHistory WHERE Id = 91;"), Is.Zero);
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Commands;"), Is.Zero);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books;"), Is.EqualTo(2), "a restart must not repeat the conversion");
        }

        [Test]
        public void should_preserve_booklore_notification_settings_and_release_import_trigger()
        {
            const string settings = "{\"BaseUrl\":\"https://booklore.example\",\"Username\":\"migration-fixture\",\"Password\":\"fixture-only-password\"}";
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Notifications (Id, Name, OnGrab, Settings, Implementation, ConfigContract, OnUpgrade, Tags, OnRename, OnReleaseImport, OnHealthIssue, IncludeHealthWarnings, OnDownloadFailure, OnImportFailure, OnBookRetag, OnAuthorDelete, OnBookDelete, OnBookFileDelete, OnBookFileDeleteForUpgrade, OnApplicationUpdate, OnAuthorAdded)
                    VALUES (113, 'BookLore', 0, @settings, @implementation, @configContract, 0, '[]', 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);";
                command.Parameters.AddWithValue("@settings", settings);
                command.Parameters.AddWithValue("@implementation", typeof(BookLore).Name);
                command.Parameters.AddWithValue("@configContract", typeof(BookLoreSettings).Name);
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            const string preservedRow = @"SELECT COUNT(*) FROM Notifications
                WHERE Id = 113 AND Name = 'BookLore' AND Implementation = 'BookLore'
                    AND ConfigContract = 'BookLoreSettings'
                    AND Settings = '{""BaseUrl"":""https://booklore.example"",""Username"":""migration-fixture"",""Password"":""fixture-only-password""}'
                    AND OnReleaseImport = 1;";

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, preservedRow), Is.EqualTo(1), "the ChaptarrNG database must retain the BookLore provider, credentials, and import trigger");
                Assert.That(QueryScalar<int>(backupPath, preservedRow), Is.EqualTo(1), "the retained BookshelfNG backup must preserve the original BookLore settings and trigger");
            });
        }

        [Test]
        public void should_preserve_supplemental_catalog_ids_and_aliases_while_retaining_the_original_source()
        {
            const string sourceAuthorId = "person/123:456";
            const string sourceBookId = "work/123:456";
            const string sourceEditionId = "volume/123:456";
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    UPDATE AuthorMetadata SET ForeignAuthorId = 'loc-author:person/123:456' WHERE Id = 10;
                    UPDATE Books SET ForeignBookId = 'loc:work/123:456' WHERE Id = 30;
                    UPDATE Editions SET ForeignEditionId = 'googlebooks:volume/123:456' WHERE Id = 40;";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var authorProviderId = AdditionalMetadataSources.CreateProviderId("loc-author", sourceAuthorId);
            var bookProviderId = AdditionalMetadataSources.CreateProviderId("loc", sourceBookId);
            var editionProviderId = AdditionalMetadataSources.CreateProviderId("googlebooks", sourceEditionId);
            var authorNormalizedId = authorProviderId.Substring("meta:".Length);
            var bookNormalizedId = bookProviderId.Substring("meta:".Length);
            var editionNormalizedId = editionProviderId.Substring("meta:".Length);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT AdditionalMetadataProviderId FROM Authors WHERE Id = 20;"), Is.EqualTo(authorProviderId));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT AdditionalMetadataProviderId FROM Books WHERE Id = 30;"), Is.EqualTo(bookProviderId));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT ForeignEditionId FROM Editions WHERE Id = 40;"), Is.EqualTo(editionProviderId));
                Assert.That(QueryScalar<int>(_sourcePath, $"SELECT COUNT(*) FROM ProviderAliasIndex WHERE EntityType = 'Author' AND EntityId = 20 AND Scope = 'author' AND Provider = 'meta' AND NormalizedProviderId = '{authorNormalizedId}';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, $"SELECT COUNT(*) FROM ProviderAliasIndex WHERE EntityType = 'Book' AND EntityId = 30 AND Scope = 'work' AND Provider = 'meta' AND NormalizedProviderId = '{bookNormalizedId}';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, $"SELECT COUNT(*) FROM ProviderAliasIndex WHERE EntityType = 'Edition' AND EntityId = 40 AND Scope = 'edition' AND Provider = 'meta' AND NormalizedProviderId = '{editionNormalizedId}';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(backupPath, "SELECT ForeignAuthorId FROM AuthorMetadata WHERE Id = 10;"), Is.EqualTo("loc-author:person/123:456"));
                Assert.That(QueryScalar<string>(backupPath, "SELECT ForeignBookId FROM Books WHERE Id = 30;"), Is.EqualTo("loc:work/123:456"));
                Assert.That(QueryScalar<string>(backupPath, "SELECT ForeignEditionId FROM Editions WHERE Id = 40;"), Is.EqualTo("googlebooks:volume/123:456"));
            });
        }

        [Test]
        public void should_preserve_supplemental_catalog_preferences_and_credentials()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO Config (Key, Value) VALUES
                        ('MetadataCatalogSources', 'openlibrary,googlebooks'),
                        ('GoogleBooksApiKey', 'fixture-key-preserved'),
                        ('MetadataTitleSourcePreference', 'openlibrary'),
                        ('MetadataCoverSourcePreference', 'googlebooks');";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'MetadataCatalogSources';"), Is.EqualTo("openlibrary,googlebooks"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'GoogleBooksApiKey';"), Is.EqualTo("fixture-key-preserved"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'MetadataTitleSourcePreference';"), Is.EqualTo("openlibrary"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'MetadataCoverSourcePreference';"), Is.EqualTo("googlebooks"));
                Assert.That(QueryScalar<string>(backupPath, "SELECT Value FROM Config WHERE Key = 'GoogleBooksApiKey';"), Is.EqualTo("fixture-key-preserved"));
            });
        }

        [Test]
        public void should_migrate_a_saved_bookshelf_hardcover_token_to_the_chaptarr_setting()
        {
            AddConfigValue("HardcoverAuth", "Bearer fixture-hardcover-token");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverApiToken';"), Is.EqualTo("fixture-hardcover-token"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverAuth';"), Is.EqualTo("Bearer fixture-hardcover-token"));
                Assert.That(QueryScalar<string>(backupPath, "SELECT Value FROM Config WHERE Key = 'HardcoverAuth';"), Is.EqualTo("Bearer fixture-hardcover-token"));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Config WHERE Key = 'HardcoverApiToken';"), Is.Zero);
            });
        }

        [Test]
        public void should_preserve_an_explicit_chaptarr_hardcover_token_over_the_legacy_value()
        {
            AddConfigValue("HardcoverAuth", "fixture-legacy-token");
            AddConfigValue("HardcoverApiToken", "fixture-explicit-token");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverApiToken';"), Is.EqualTo("fixture-explicit-token"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverAuth';"), Is.EqualTo("fixture-legacy-token"));
            });
        }

        [Test]
        public void should_preserve_an_explicitly_cleared_chaptarr_hardcover_token()
        {
            AddConfigValue("HardcoverAuth", "fixture-legacy-token");
            AddConfigValue("HardcoverApiToken", string.Empty);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverApiToken';"), Is.Empty);
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'HardcoverAuth';"), Is.EqualTo("fixture-legacy-token"));
            });
        }

        [Test]
        public void should_translate_the_legacy_match_threshold_and_retain_its_original_value()
        {
            AddConfigValue("bookimportminimummatchpercent", "70");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'bookmatchingstrictness';"), Is.EqualTo("balanced"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'bookimportminimummatchpercent';"), Is.EqualTo("70"));
                Assert.That(QueryScalar<string>(backupPath, "SELECT Value FROM Config WHERE Key = 'bookimportminimummatchpercent';"), Is.EqualTo("70"));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Config WHERE Key = 'bookmatchingstrictness';"), Is.Zero);
            });
        }

        [Test]
        public void should_preserve_an_explicit_chaptarr_match_mode_over_the_legacy_threshold()
        {
            AddConfigValue("bookimportminimummatchpercent", "50");
            AddConfigValue("bookmatchingstrictness", "strict");

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'bookmatchingstrictness';"), Is.EqualTo("strict"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'bookimportminimummatchpercent';"), Is.EqualTo("50"));
            });
        }

        [Test]
        public void should_reject_an_invalid_legacy_threshold_even_when_an_explicit_mode_exists()
        {
            AddConfigValue("bookimportminimummatchpercent", "100");
            AddConfigValue("bookmatchingstrictness", "strict");
            var sourceBeforeConversion = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() =>
                new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("integer from 50 through 99"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBeforeConversion));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_reject_an_out_of_range_legacy_match_threshold_without_changing_the_source()
        {
            AddConfigValue("bookimportminimummatchpercent", "100");
            var sourceBeforeConversion = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() =>
                new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("integer from 50 through 99"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBeforeConversion));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_upgrade_an_older_bookshelf_snapshot_and_keep_the_original_schema_for_rollback()
        {
            DowngradeSourceToMigration22();
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);

            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            var backupPath = result.BackupPath;
            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31);"), Is.EqualTo(2));
                Assert.That(QueryScalar<int>(backupPath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryRows(backupPath, "SELECT name FROM pragma_table_info('Authors');"), Does.Not.Contain("EbookPath"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });

            var restored = new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                backupPath,
                migrateBookshelfSource: migrationController.MigrateBookshelfSource);

            Assert.Multiple(() =>
            {
                Assert.That(restored.PreviewOnly, Is.False);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31);"), Is.EqualTo(1));
                Assert.That(File.Exists(restored.ChaptarrBackupPath), Is.True);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
            });
        }

        [Test]
        public void should_preview_older_schema_upgrade_without_modifying_the_source_or_leaving_staging_files()
        {
            DowngradeSourceToMigration22();
            var sourceBefore = File.ReadAllBytes(_sourcePath);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);

            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.True);
                Assert.That(result.BackupPath, Is.Null);
                Assert.That(result.ImportReport.Tables.Count, Is.GreaterThan(0));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(sourceBefore));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-migrate-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.chaptarr-stage-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-snapshot-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_upgrade_a_genuine_migration_22_schema_created_by_bookshelf_migrations()
        {
            CreateSourceThroughMigration(22);
            SeedMigration22SourceWithLibrary();

            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);
            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(result.BackupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<int>(result.BackupPath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryScalar<int>(result.BackupPath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND Title = 'Migration 22 fixture book';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(result.BackupPath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND IsEbook = 0;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(result.BackupPath, "SELECT Path FROM BookFiles WHERE Id = 50;"), Is.EqualTo("/library/Migration 22 Author/book.m4b"));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND Title = 'Migration 22 fixture book' AND MediaType = 0 AND AudiobookMonitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Path FROM BookFiles WHERE Id = 50;"), Is.EqualTo("/library/Migration 22 Author/book.m4b"));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("preserved"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });

            var restored = new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                result.BackupPath,
                migrateBookshelfSource: migrationController.MigrateBookshelfSource);

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(22));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND Title = 'Migration 22 fixture book';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(File.Exists(restored.ChaptarrBackupPath), Is.True);
            });
        }

        [Test]
        public void should_upgrade_the_oldest_bookshelf_schema_and_keep_the_original_schema_for_rollback()
        {
            CreateSourceThroughMigration(1);
            var migrationController = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null);

            var result = migrationController.MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<int>(result.BackupPath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books;"), Is.Zero);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });

            var restored = new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                result.BackupPath,
                migrateBookshelfSource: migrationController.MigrateBookshelfSource);

            Assert.Multiple(() =>
            {
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT MAX(Version) FROM VersionInfo;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books;"), Is.Zero);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(File.Exists(restored.ChaptarrBackupPath), Is.True);
            });
        }

        [Test]
        public void should_reject_a_legacy_table_alias_after_its_bookshelf_rename_migration()
        {
            DowngradeSourceToMigration22();
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "ALTER TABLE Blocklist RENAME TO Blacklist;";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).MigrateBookshelf(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite,
                    previewOnly: false));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("unknown: Blacklist"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_reject_a_bookshelf_schema_newer_than_the_embedded_migration_chain()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE VersionInfo SET Version = 159 WHERE Version = 158;";
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("outside the bundled migration chain (1-158)"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-migrate-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_preview_bookshelf_rollback_without_changing_either_database()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var currentBefore = File.ReadAllBytes(_sourcePath);
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            var result = new BookshelfDatabaseConverter().Restore(_sourceConnectionString, bookshelfBackupPath, previewOnly: true);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.True);
                Assert.That(result.BookshelfBackupPath, Is.EqualTo(bookshelfBackupPath));
                Assert.That(result.ChaptarrBackupPath, Is.Null);
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(currentBefore));
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "readarr.db.chaptarrng-backup-*.db"), Is.Empty);
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-restore-stage-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_restore_the_source_and_retain_chaptarr_writes_for_rollback()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            using (var current = new SqliteConnection(_sourceConnectionString))
            {
                current.Open();
                using var command = current.CreateCommand();
                command.CommandText = "UPDATE Config SET Value = 'after-migration' WHERE Key = 'FixtureSetting';";
                Assert.That(command.ExecuteNonQuery(), Is.EqualTo(1));
            }

            var result = new BookshelfDatabaseConverter().Restore(_sourceConnectionString, bookshelfBackupPath);

            Assert.Multiple(() =>
            {
                Assert.That(result.PreviewOnly, Is.False);
                Assert.That(File.Exists(result.ChaptarrBackupPath), Is.True);
                Assert.That(DatabaseLineageGuard.Detect(_sourceConnectionString, DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(result.ChaptarrBackupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.Chaptarr));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("preserved"));
                Assert.That(QueryScalar<string>(result.ChaptarrBackupPath, "SELECT Value FROM Config WHERE Key = 'FixtureSetting';"), Is.EqualTo("after-migration"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(QueryScalar<string>(result.ChaptarrBackupPath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(result.ChaptarrBackupPath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelf-restore-stage-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_bookshelf_backup_with_the_wrong_identity_without_changing_the_current_database()
        {
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);
            var bookshelfBackupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var wrongIdentityPath = Path.Combine(_directory, "unrelated.db");
            File.Copy(bookshelfBackupPath, wrongIdentityPath);
            var currentBefore = File.ReadAllBytes(_sourcePath);
            var sourceBackupBefore = File.ReadAllBytes(bookshelfBackupPath);

            var exception = Assert.Throws<InvalidDataException>(() => new BookshelfDatabaseConverter().Restore(
                _sourceConnectionString,
                wrongIdentityPath));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("not a retained BookshelfNG conversion backup"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(currentBefore));
                Assert.That(File.ReadAllBytes(bookshelfBackupPath), Is.EqualTo(sourceBackupBefore));
                Assert.That(Directory.GetFiles(_directory, "readarr.db.chaptarrng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_an_unsupported_bookshelf_identity_without_changing_the_source()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE AuthorMetadata SET ForeignAuthorId = 'metadata-api-author:opaque-10' WHERE Id = 10;";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("no recognized provider prefix"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        [Test]
        public void should_preserve_one_monitored_edition_for_each_media_format()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 1 WHERE Id = 41;";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 1 AND EbookMonitored = 0 AND ForeignEditionId = 'hc:audio-40';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND AudiobookMonitored = 0 AND EbookMonitored = 1 AND ForeignEditionId = 'ol:OL123M';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 40 AND BookId = 30 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE Id = 41 AND BookId = 31 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_multiple_monitored_editions_of_the_same_media_format()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET IsEbook = 0, Monitored = 1;";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("more than one monitored audiobook edition"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        [Test]
        public void should_preserve_a_monitored_mixed_format_book_without_a_selected_edition_as_both_unmonitored()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var reviewPath = _sourcePath + ".bookshelf-media-review.json";
            var review = File.ReadAllText(reviewPath);
            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (30, 31) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Editions WHERE BookId IN (30, 31);"), Is.EqualTo(2));
                Assert.That(review, Does.Contain("\"sourceBookId\": 30"));
                Assert.That(review, Does.Contain("\"mediaType\": \"both-unmonitored\""));
                Assert.That(review, Does.Contain("\"monitored\": true"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                if (!OperatingSystem.IsWindows())
                {
                    Assert.That(File.GetUnixFileMode(reviewPath), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
                }
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND Monitored = 1;"), Is.EqualTo(1));
            });
        }

        [Test]
        public void should_automatically_preserve_a_book_without_editions_in_both_catalogs_unmonitored()
        {
            InsertBookWithoutEdition(32, monitored: true);
            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            var backupPath = Directory.GetFiles(_directory, "readarr.db.bookshelfng-backup-*.db").Single();
            var reviewPath = _sourcePath + ".bookshelf-media-review.json";
            var review = File.ReadAllText(reviewPath);
            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(File.Exists(reviewPath), Is.True);
                Assert.That(review, Does.Contain("\"sourceBookId\": 32"));
                Assert.That(review, Does.Contain("\"mediaType\": \"both-unmonitored\""));
                Assert.That(review, Does.Contain("\"monitored\": true"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
                Assert.That(DatabaseLineageGuard.Detect(ConnectionStringFor(backupPath), DatabaseType.SQLite), Is.EqualTo(DatabaseLineage.BookshelfNG));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Books WHERE Id = 32 AND Monitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(backupPath, "SELECT COUNT(*) FROM Editions WHERE BookId = 32;"), Is.Zero);
            });
        }

        [Test]
        public void should_accept_an_explicit_both_unmonitored_media_choice()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "both-unmonitored" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34) AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(2));
                Assert.That(File.Exists(_sourcePath + ".bookshelf-media-review.json"), Is.False);
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_import_a_book_without_editions_using_the_explicit_both_formats_choice()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-review.json", "{\"version\":1,\"books\":[]}");
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "both" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id IN (32, 34);"), Is.EqualTo(2));
                Assert.That(File.Exists(_sourcePath + ".bookshelf-media-review.json"), Is.False);
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 32 AND MediaType = 0 AND AudiobookMonitored = 1 AND EbookMonitored = 0;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 34 AND MediaType = 1 AND AudiobookMonitored = 0 AND EbookMonitored = 1;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_apply_an_explicit_monitoring_choice_to_a_mixed_format_book_without_a_selected_edition()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 30 AND MediaType = 0 AND AudiobookMonitored = 0 AND EbookMonitored = 0;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Books WHERE Id = 31 AND MediaType = 1 AND EbookMonitored = 1 AND ForeignEditionId IS NULL;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_preserve_unmapped_source_files_without_inventing_media_or_edition_relationships()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    UPDATE BookFiles SET EditionId = 0 WHERE Id = 50;
                    INSERT INTO ExtraFiles (Id, AuthorId, BookId, BookFileId, RelativePath, Extension, Added, LastUpdated)
                    VALUES (62, 20, 30, 50, 'linked-cover.jpg', 'jpg', '2026-01-05', '2026-01-05');
                    INSERT INTO MetadataFiles (Id, AuthorId, Consumer, Type, RelativePath, LastUpdated, BookId, BookFileId, Extension)
                    VALUES (63, 20, 'AudioBookShelf', 0, 'unmapped.opf', '2026-01-05', 30, 50, 'opf');
                    INSERT INTO Metadata (Id, Enable, Name, Implementation, Settings, ConfigContract)
                    VALUES (101, 1, 'Legacy OPF sidecars', 'AudiobookshelfMetadata', '{}', 'NullConfig');
                    INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                    VALUES (94, 'book.m4b', '2026-01-05', '{}', '{""FileId"":""50"",""ImportedPath"":""/library/Fixture Author/book.m4b""}', 3, 'download-94', 20, 30);";
                command.ExecuteNonQuery();
            }

            new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM BookFiles WHERE Id = 50 AND EditionId = 0 AND MediaType = '' AND UnmappedReason LIKE '%not inferred%';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Path FROM BookFiles WHERE Id = 50;"), Is.EqualTo("/library/Fixture Author/book.m4b"));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM ExtraFiles WHERE Id = 62 AND BookId = 30 AND BookFileId = 50;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM MetadataFiles WHERE Id = 63 AND BookId = 30 AND BookFileId = 50;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Consumer FROM MetadataFiles WHERE Id = 63;"), Is.EqualTo("AudiobookshelfMetadata"));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM Metadata WHERE Id = 101 AND Enable = 1 AND Implementation = 'AudiobookshelfMetadata' AND ConfigContract = 'NullConfig';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM History WHERE Id = 94 AND BookId = 30 AND EditionId IS NULL AND Data LIKE '%\"FileId\":\"50\"%';"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_preserve_book_files_that_reference_a_missing_edition_as_unmapped()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO BookFiles (Id, EditionId, CalibreId, Quality, Size, DateAdded, MediaInfo, Modified, Path, Part)
                    VALUES (52, 9999, 0, '{}', 300, '2026-01-04', '{}', '2026-01-04', '/library/Fixture Author/unmapped.m4b', 0);
                    INSERT INTO ExtraFiles (Id, AuthorId, BookId, BookFileId, RelativePath, Extension, Added, LastUpdated)
                    VALUES (64, 20, 30, 52, 'unmapped-cover.jpg', 'jpg', '2026-01-06', '2026-01-06');
                    INSERT INTO MetadataFiles (Id, AuthorId, Consumer, Type, RelativePath, LastUpdated, BookId, BookFileId, Extension)
                    VALUES (65, 20, 'AudioBookShelf', 0, 'unmapped.opf', '2026-01-06', 30, 52, 'opf');
                    INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                    VALUES (95, 'unmapped.m4b', '2026-01-06', '{}', '{""FileId"":""52"",""ImportedPath"":""/library/Fixture Author/unmapped.m4b""}', 3, 'download-95', 20, 30);";
                command.ExecuteNonQuery();
            }

            var result = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM BookFiles WHERE Id = 52 AND EditionId = 0 AND MediaType = '' AND UnmappedReason LIKE '%missing edition%' AND Path = '/library/Fixture Author/unmapped.m4b';"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM ExtraFiles WHERE Id = 64 AND BookId = 30 AND BookFileId = 52;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM MetadataFiles WHERE Id = 65 AND BookId = 30 AND BookFileId = 52;"), Is.EqualTo(1));
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM History WHERE Id = 95 AND BookId = 30 AND EditionId IS NULL AND Data LIKE '%\"FileId\":\"52\"%';"), Is.EqualTo(1));
                Assert.That(result.ImportReport.Tables["BookFiles"].TargetMapping, Does.Contain("missing edition 1"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_preserve_history_with_a_cross_book_file_reference_without_relinking_it()
        {
            InsertBookWithoutEdition(32, monitored: true);
            const string historyData = "{\"FileId\":\"50\",\"ImportedPath\":\"/library/Fixture Author/book.m4b\"}";
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = @"
                    INSERT INTO History (Id, SourceTitle, Date, Quality, Data, EventType, DownloadId, AuthorId, BookId)
                    VALUES (96, 'book.m4b', '2026-01-07', '{}', $data, 3, 'download-96', 20, 32);";
                command.Parameters.AddWithValue("$data", historyData);
                command.ExecuteNonQuery();
            }

            var result = new MigrationController(LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).MigrateBookshelf(
                _sourceConnectionString,
                new MigrationContext(MigrationType.Main),
                DatabaseType.SQLite,
                previewOnly: false);

            Assert.Multiple(() =>
            {
                Assert.That(QueryScalar<int>(_sourcePath, "SELECT COUNT(*) FROM History WHERE Id = 96 AND BookId = 32 AND EditionId IS NULL;"), Is.EqualTo(1));
                Assert.That(QueryScalar<string>(_sourcePath, "SELECT Data FROM History WHERE Id = 96;"), Is.EqualTo(historyData));
                Assert.That(result.ImportReport.Tables["History"].TargetMapping, Does.Contain("cross-book file references: 1"));
                Assert.That(result.ImportReport.Tables["History"].TargetMapping, Does.Contain("remain unlinked"));
                Assert.That(QueryScalar<string>(_sourcePath, "PRAGMA integrity_check;"), Is.EqualTo("ok"));
                Assert.That(QueryRows(_sourcePath, "PRAGMA foreign_key_check;"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_an_incomplete_media_map_without_changing_the_source()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": []
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("1 BookshelfNG book(s) need an explicit media type"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_stale_media_map_when_the_current_source_has_no_ambiguous_books()
        {
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));
            var review = File.ReadAllText(_sourcePath + ".bookshelf-media-review.json");

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("not ambiguous in the current source"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(review, Does.Contain("\"books\""));
                Assert.That(review, Does.Not.Contain("\"sourceBookId\": 30"));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_keep_completed_media_choices_in_the_review_when_a_map_is_incomplete()
        {
            InsertBookWithoutEdition(32, monitored: true);
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "UPDATE Editions SET Monitored = 0;";
                command.ExecuteNonQuery();
            }

            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 30, "mediaType": "ebook" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));
            var review = File.ReadAllText(_sourcePath + ".bookshelf-media-review.json");

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("1 BookshelfNG book(s) need an explicit media type"));
                Assert.That(review, Does.Contain("\"sourceBookId\": 30"));
                Assert.That(review, Does.Contain("\"mediaType\": \"ebook\""));
                Assert.That(review, Does.Contain("\"sourceBookId\": 32"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_a_media_map_with_an_invalid_choice_without_changing_the_source()
        {
            InsertBookWithoutEdition(32, monitored: true);
            File.WriteAllText(_sourcePath + ".bookshelf-media-map.json", """
                {
                  "version": 1,
                  "books": [
                    { "sourceBookId": 32, "mediaType": "audio" }
                  ]
                }
                """);
            var before = File.ReadAllBytes(_sourcePath);

            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.Multiple(() =>
            {
                Assert.That(exception.Message, Does.Contain("invalid book ID or mediaType"));
                Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
            });
        }

        [Test]
        public void should_refuse_nonempty_unmapped_setting_columns_without_changing_the_source()
        {
            using (var source = new SqliteConnection(_sourceConnectionString))
            {
                source.Open();
                using var command = source.CreateCommand();
                command.CommandText = "ALTER TABLE Config ADD COLUMN UnmappedValue TEXT; UPDATE Config SET UnmappedValue = 'keep-me' WHERE Key = 'FixtureSetting';";
                command.ExecuteNonQuery();
            }

            var before = File.ReadAllBytes(_sourcePath);
            var exception = Assert.Throws<InvalidDataException>(() => new MigrationController(
                LogManager.GetLogger(nameof(BookshelfDatabaseImporterFixture)), null).Migrate(
                    _sourceConnectionString,
                    new MigrationContext(MigrationType.Main),
                    DatabaseType.SQLite));

            Assert.That(exception.Message, Does.Contain("Config.UnmappedValue"));
            Assert.That(File.ReadAllBytes(_sourcePath), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(_directory, "*.bookshelfng-backup-*.db"), Is.Empty);
        }

        private void CreateSourceDatabase()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(x => x.EndsWith("BookshelfNG.Schema.158.sql", StringComparison.Ordinal));
            using var schema = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(schema);
            using var connection = new SqliteConnection(_sourceConnectionString);
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = reader.ReadToEnd();
            create.ExecuteNonQuery();

            var seedResourceName = assembly.GetManifestResourceNames().Single(x => x.EndsWith("BookshelfNG.PopulatedMigration158.sql", StringComparison.Ordinal));
            using var seed = assembly.GetManifestResourceStream(seedResourceName);
            using var seedReader = new StreamReader(seed);
            using var data = connection.CreateCommand();
            data.CommandText = seedReader.ReadToEnd();
            data.ExecuteNonQuery();
        }

        private void SetBareNumericProviderIds()
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                UPDATE AuthorMetadata SET ForeignAuthorId = '10' WHERE Id = 10;
                UPDATE Books SET ForeignBookId = '30' WHERE Id = 30;
                UPDATE Editions SET ForeignEditionId = '40' WHERE Id = 40;
                UPDATE Series SET ForeignSeriesId = '70' WHERE Id = 70;";
            command.ExecuteNonQuery();
        }

        private void InsertBookWithoutEdition(int id, bool monitored)
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                INSERT INTO Books (Id, AuthorMetadataId, ForeignBookId, TitleSlug, Title, Links, Genres, Ratings, CleanTitle, Monitored, AnyEditionOk, Added, RelatedBooks, LastSearchTime)
                VALUES ($id, 10, $foreignId, $slug, 'Unresolved fixture book', '[]', '[]', '{}', 'unresolved fixture book', $monitored, 0, '2026-01-05', '[]', NULL);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$foreignId", $"hardcover:book-{id}");
            command.Parameters.AddWithValue("$slug", $"no-edition-{id}");
            command.Parameters.AddWithValue("$monitored", monitored ? 1 : 0);
            command.ExecuteNonQuery();
        }

        private void DowngradeSourceToMigration22()
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                ALTER TABLE Authors DROP COLUMN EbookMetadataProfileId;
                ALTER TABLE Authors DROP COLUMN AudiobookMetadataProfileId;
                ALTER TABLE Authors DROP COLUMN EbookQualityProfileId;
                ALTER TABLE Authors DROP COLUMN AudiobookQualityProfileId;
                ALTER TABLE Authors DROP COLUMN EbookPath;
                ALTER TABLE Authors DROP COLUMN AudiobookPath;
                ALTER TABLE Editions DROP COLUMN Narrator;
                ALTER TABLE MetadataProfiles DROP COLUMN PreferredSeries;
                ALTER TABLE MetadataProfiles DROP COLUMN PreferredEditionTerms;
                ALTER TABLE BookFiles DROP COLUMN LastRemoteSearchTime;
                ALTER TABLE BookFiles DROP COLUMN IndexerFlags;
                ALTER TABLE Blocklist DROP COLUMN IndexerFlags;
                ALTER TABLE Books DROP COLUMN LastSearchTime;
                ALTER TABLE Notifications DROP COLUMN OnAuthorAdded;
                DROP TABLE NotificationStatus;
                ALTER TABLE Commands DROP COLUMN Result;
                ALTER TABLE DownloadClients DROP COLUMN Tags;
                ALTER TABLE DownloadClients DROP COLUMN RemoveCompletedDownloads;
                ALTER TABLE DownloadClients DROP COLUMN RemoveFailedDownloads;
                ALTER TABLE NamingConfig DROP COLUMN ColonReplacementFormat;
                ALTER TABLE Indexers DROP COLUMN DownloadClientId;
                ALTER TABLE Notifications DROP COLUMN OnApplicationUpdate;
                ALTER TABLE DelayProfiles DROP COLUMN BypassIfHighestQuality;
                ALTER TABLE DelayProfiles DROP COLUMN BypassIfAboveCustomFormatScore;
                ALTER TABLE DelayProfiles DROP COLUMN MinimumCustomFormatScore;
                ALTER TABLE BookFiles DROP COLUMN OriginalFilePath;
                ALTER TABLE QualityProfiles DROP COLUMN FormatItems;
                ALTER TABLE QualityProfiles DROP COLUMN MinFormatScore;
                ALTER TABLE QualityProfiles DROP COLUMN CutoffFormatScore;
                ALTER TABLE ReleaseProfiles ADD COLUMN Preferred TEXT;
                ALTER TABLE ReleaseProfiles ADD COLUMN IncludePreferredWhenRenaming INTEGER;
                DROP TABLE CustomFormats;
                ALTER TABLE Indexers DROP COLUMN Tags;
                ALTER TABLE ImportListStatus DROP COLUMN LastInfoSync;
                ALTER TABLE ImportListStatus ADD COLUMN LastSyncListInfo DATETIME;
                DELETE FROM VersionInfo WHERE Version > 22;";
            command.ExecuteNonQuery();
        }

        private void CreateSourceThroughMigration(long targetVersion)
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(_sourcePath))
            {
                File.Delete(_sourcePath);
            }

            using var serviceProvider = new ServiceCollection()
                .AddLogging(builder => builder.AddNLog())
                .AddFluentMigratorCore()
                .Configure<RunnerOptions>(options => options.IncludeUntaggedMaintenances = true)
                .ConfigureRunner(builder => builder
                    .AddNzbDroneSQLite()
                    .WithGlobalConnectionString(_sourceConnectionString)
                    .ScanIn(typeof(MigrationController).Assembly).For.All())
                .Configure<TypeFilterOptions>(options => options.Namespace = "NzbDrone.Core.Datastore.BookshelfMigration")
                .Configure<ProcessorOptions>(options =>
                {
                    options.PreviewOnly = false;
                    options.Timeout = TimeSpan.FromMinutes(5);
                })
                .Configure<SelectingProcessorAccessorOptions>(options => options.ProcessorId = "SQLite")
                .Configure<SelectingGeneratorAccessorOptions>(options => options.GeneratorId = "SQLite")
                .BuildServiceProvider();

            using var scope = serviceProvider.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
            MigrationContext.Current = new MigrationContext(MigrationType.Main);
            try
            {
                runner.MigrateUp(targetVersion);
            }
            finally
            {
                MigrationContext.Current = null;
            }
        }

        private void SeedMigration22SourceWithLibrary()
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = @"
                INSERT INTO Config (Key, Value) VALUES ('FixtureSetting', 'preserved');
                INSERT INTO QualityProfiles (Id, Name, Cutoff, Items, UpgradeAllowed)
                VALUES (1, 'Migration 22 profile', 1, '[]', 1);
                INSERT INTO MetadataProfiles (Id, Name, MinPopularity, SkipMissingDate, SkipMissingIsbn, SkipPartsAndSets, SkipSeriesSecondary, AllowedLanguages)
                VALUES (1, 'Migration 22 metadata', 0, 0, 0, 0, 0, '[]');
                INSERT INTO RootFolders (Id, Path, Name, DefaultMetadataProfileId, DefaultQualityProfileId, DefaultMonitorOption, DefaultTags, IsCalibreLibrary)
                VALUES (1, '/library', 'Library', 1, 1, 0, '[]', 0);
                INSERT INTO AuthorMetadata (Id, ForeignAuthorId, TitleSlug, Name, Overview, Status, Images, Aliases, SortName, NameLastFirst, SortNameLastFirst)
                VALUES (10, 'hardcover-author:author-10', 'migration-22-author', 'Migration 22 Author', 'Older-schema author', 0, '[]', '[]', 'Migration 22 Author', 'Author, Migration 22', 'Author, Migration 22');
                INSERT INTO Authors (Id, CleanName, Path, Monitored, QualityProfileId, Tags, Added, MetadataProfileId, AuthorMetadataId)
                VALUES (20, 'migration22author', '/library/Migration 22 Author', 1, 1, '[]', '2026-01-01', 1, 10);
                INSERT INTO Books (Id, AuthorMetadataId, ForeignBookId, TitleSlug, Title, Links, Genres, Ratings, CleanTitle, Monitored, AnyEditionOk, Added)
                VALUES (30, 10, 'hardcover:book-30', 'migration-22-book', 'Migration 22 fixture book', '[]', '[]', '{}', 'migration 22 fixture book', 1, 0, '2026-01-02');
                INSERT INTO Editions (Id, BookId, ForeignEditionId, Title, TitleSlug, Images, IsEbook, Monitored, ManualAdd)
                VALUES (40, 30, 'hardcover-edition:edition-40', 'Migration 22 audiobook', 'migration-22-edition', '[]', 0, 1, 0);
                INSERT INTO BookFiles (Id, EditionId, CalibreId, Quality, Size, DateAdded, MediaInfo, Path)
                VALUES (50, 40, 0, '{}', 1024, '2026-01-02', '{}', '/library/Migration 22 Author/book.m4b');";
            command.ExecuteNonQuery();
        }

        private static string ConnectionStringFor(string path) => new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;

        private void AddConfigValue(string key, string value)
        {
            using var source = new SqliteConnection(_sourceConnectionString);
            source.Open();
            using var command = source.CreateCommand();
            command.CommandText = "INSERT INTO Config (Key, Value) VALUES (@key, @value);";
            command.Parameters.AddWithValue("@key", key);
            command.Parameters.AddWithValue("@value", value);
            command.ExecuteNonQuery();
        }

        private static T QueryScalar<T>(string databasePath, string sql)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return (T)Convert.ChangeType(command.ExecuteScalar(), typeof(T));
        }

        private static object[] QueryRows(string databasePath, string sql)
        {
            using var connection = new SqliteConnection(ConnectionStringFor(databasePath));
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new System.Collections.Generic.List<object>();
            while (reader.Read()) rows.Add(reader.GetValue(0));
            return rows.ToArray();
        }
    }
}
