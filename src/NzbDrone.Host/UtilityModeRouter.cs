using System;
using System.IO;
using System.Linq;
using NLog;
using NLog.Extensions.Logging;
using NzbDrone.Common;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Processes;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Migration.Framework;
using NzbDrone.Host.AccessControl;
using IServiceProvider = NzbDrone.Common.IServiceProvider;

namespace NzbDrone.Host
{
    public interface IUtilityModeRouter
    {
        void Route(ApplicationModes applicationModes);
    }

    public class UtilityModeRouter : IUtilityModeRouter
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConsoleService _consoleService;
        private readonly IProcessProvider _processProvider;
        private readonly IRemoteAccessAdapter _remoteAccessAdapter;
        private readonly IAppFolderFactory _appFolderFactory;
        private readonly IConnectionStringFactory _connectionStringFactory;
        private readonly IConfigService _configService;
        private readonly IStartupContext _startupContext;
        private readonly Logger _logger;

        public UtilityModeRouter(IServiceProvider serviceProvider,
                      IConsoleService consoleService,
                      IProcessProvider processProvider,
                      IRemoteAccessAdapter remoteAccessAdapter,
                      IAppFolderFactory appFolderFactory,
                      IConnectionStringFactory connectionStringFactory,
                      IConfigService configService,
                      IStartupContext startupContext,
                      Logger logger)
        {
            _serviceProvider = serviceProvider;
            _consoleService = consoleService;
            _processProvider = processProvider;
            _remoteAccessAdapter = remoteAccessAdapter;
            _appFolderFactory = appFolderFactory;
            _connectionStringFactory = connectionStringFactory;
            _configService = configService;
            _startupContext = startupContext;
            _logger = logger;
        }

        public void Route(ApplicationModes applicationModes)
        {
            _logger.Info("Application mode: {0}", applicationModes);

            switch (applicationModes)
            {
                case ApplicationModes.InstallService:
                    {
                        _logger.Debug("Install Service selected");
                        if (_serviceProvider.ServiceExist(ServiceProvider.SERVICE_NAME))
                        {
                            _consoleService.PrintServiceAlreadyExist();
                        }
                        else
                        {
                            _remoteAccessAdapter.MakeAccessible(true);
                            _serviceProvider.Install(ServiceProvider.SERVICE_NAME);
                            _serviceProvider.SetPermissions(ServiceProvider.SERVICE_NAME);

                            // Start the service and exit.
                            // Ensures that there isn't an instance of Chaptarr already running that the service account cannot stop.
                            _processProvider.SpawnNewProcess("sc.exe", $"start {ServiceProvider.SERVICE_NAME}", null, true);
                        }

                        break;
                    }

                case ApplicationModes.UninstallService:
                    {
                        _logger.Debug("Uninstall Service selected");
                        if (!_serviceProvider.ServiceExist(ServiceProvider.SERVICE_NAME))
                        {
                            _consoleService.PrintServiceDoesNotExist();
                        }
                        else
                        {
                            _serviceProvider.Uninstall(ServiceProvider.SERVICE_NAME);
                        }

                        break;
                    }

                case ApplicationModes.RegisterUrl:
                    {
                        _logger.Debug("Regiser URL selected");
                        _remoteAccessAdapter.MakeAccessible(false);
                        _appFolderFactory.SetPermissions();

                        break;
                    }

                case ApplicationModes.MigrateBookshelf:
                    {
                        _appFolderFactory.Register();
                        var database = _connectionStringFactory.MainDbConnection;
                        if (database.DatabaseType is not (DatabaseType.SQLite or DatabaseType.PostgreSQL) ||
                            DatabaseLineageGuard.Detect(database.ConnectionString, database.DatabaseType) != DatabaseLineage.BookshelfNG)
                        {
                            throw new InvalidDataException("No supported BookshelfNG SQLite or PostgreSQL database was found in this data directory. No database was changed.");
                        }

                        var previewOnly = _startupContext.PreviewBookshelfMigration;
                        if (!previewOnly && !_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException(
                                "Applying a BookshelfNG migration requires -confirm-bookshelf-service-stopped. Stop every process using this data directory first.");
                        }

                        var migrationController = new MigrationController(_logger, new NLogLoggerProvider(), new AppFolderInfo(_startupContext));
                        var result = migrationController.MigrateBookshelf(
                            database.ConnectionString,
                            new MigrationContext(MigrationType.Main),
                            database.DatabaseType,
                            previewOnly);

                        Console.WriteLine(previewOnly
                            ? "BookshelfNG migration preview passed. The source database was not changed."
                            : $"BookshelfNG migration completed. Backup retained at: {result.BackupPath}");
                        Console.WriteLine("Source rows by table: source | preserved | transformed | rebuilt | reset");
                        foreach (var table in result.ImportReport.Tables.OrderBy(item => item.Key, StringComparer.Ordinal))
                        {
                            var row = table.Value;
                            Console.WriteLine($"{table.Key}: {row.SourceRows} | {row.PreservedRows} | {row.TransformedRows} | {row.RebuiltRows} | {row.ResetRows} — {row.TargetMapping}");
                        }

                        foreach (var issue in result.ImportReport.BlockingIssues)
                        {
                            Console.WriteLine("BLOCKED: " + issue);
                        }

                        break;
                    }

                case ApplicationModes.CheckBookshelfMigration:
                    {
                        _appFolderFactory.Register();
                        var database = _connectionStringFactory.MainDbConnection;
                        if (database.DatabaseType is not (DatabaseType.SQLite or DatabaseType.PostgreSQL) ||
                            DatabaseLineageGuard.Detect(database.ConnectionString, database.DatabaseType) != DatabaseLineage.Chaptarr)
                        {
                            throw new InvalidDataException("BookshelfNG handoff health check did not find a readable ChaptarrNG database.");
                        }

                        if (!_configService.BookshelfNGDatabaseMigrated)
                        {
                            throw new InvalidDataException("BookshelfNG handoff health check did not find the persisted migration marker.");
                        }

                        var providerDialect = _configService.BookshelfNGProviderDialect;
                        if (!string.Equals(providerDialect, "hc", StringComparison.Ordinal) &&
                            !string.Equals(providerDialect, "gr", StringComparison.Ordinal))
                        {
                            throw new InvalidDataException("BookshelfNG handoff health check found an invalid source provider dialect.");
                        }

                        Console.WriteLine($"BookshelfNG handoff health check passed. The migrated {providerDialect} ChaptarrNG database is readable.");
                        break;
                    }

                case ApplicationModes.RestoreBookshelf:
                    {
                        _appFolderFactory.Register();
                        var database = _connectionStringFactory.MainDbConnection;
                        if (database.DatabaseType is not (DatabaseType.SQLite or DatabaseType.PostgreSQL) ||
                            DatabaseLineageGuard.Detect(database.ConnectionString, database.DatabaseType) != DatabaseLineage.Chaptarr)
                        {
                            throw new InvalidDataException("BookshelfNG rollback requires a valid ChaptarrNG SQLite or PostgreSQL database in this data directory.");
                        }

                        if (!_startupContext.Args.TryGetValue("bookshelf-backup", out var backupPath) ||
                            string.IsNullOrWhiteSpace(backupPath))
                        {
                            throw new InvalidOperationException("BookshelfNG rollback requires -bookshelf-backup=/path/to/readarr.db.bookshelfng-backup-....db for SQLite or -bookshelf-backup=bookshelfng_backup_... for PostgreSQL.");
                        }

                        var previewOnly = _startupContext.PreviewBookshelfRestore;
                        if (!previewOnly && !_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException(
                                "Restoring a BookshelfNG database requires -confirm-bookshelf-service-stopped. Stop every process using this data directory first.");
                        }

                        var migrationController = new MigrationController(_logger, new NLogLoggerProvider(), new AppFolderInfo(_startupContext));
                        var result = migrationController.RestoreBookshelf(
                            database.ConnectionString,
                            database.DatabaseType,
                            backupPath,
                            previewOnly);
                        Console.WriteLine(previewOnly
                            ? $"BookshelfNG rollback preview passed. The current ChaptarrNG database and source backup were not changed: {result.BookshelfBackupPath}"
                            : $"BookshelfNG database restored from: {result.BookshelfBackupPath}");
                        if (!previewOnly)
                        {
                            Console.WriteLine($"The previous ChaptarrNG database, including post-migration writes, is retained at: {result.ChaptarrBackupPath}");
                            Console.WriteLine("Start only BookshelfNG against this restored database. Do not start ChaptarrNG until you intentionally convert again.");
                        }

                        break;
                    }

                case ApplicationModes.CheckBookshelfPostgresBackup:
                    {
                        GetPostgresBookshelfDatabase(DatabaseLineage.BookshelfNG);
                        var databases = GetPostgresBackupDatabases();
                        var backup = new BookshelfPostgresExternalBackup(new ExternalToolRunner());
                        backup.VerifyTools();
                        foreach (var (_, database) in databases)
                        {
                            backup.ValidateDatabaseConnection(database.ConnectionString);
                        }

                        if (_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            foreach (var (_, database) in databases)
                            {
                                backup.EnsureSourceDatabaseIsIdle(database.ConnectionString);
                            }
                        }

                        Console.WriteLine("PostgreSQL external backup tools are available for the BookshelfNG main, log, and cache databases.");
                        break;
                    }

                case ApplicationModes.BackupBookshelfPostgres:
                    {
                        if (!_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException("Creating an independent PostgreSQL backup requires -confirm-bookshelf-service-stopped. Stop every process using these databases first.");
                        }

                        GetPostgresBookshelfDatabase(DatabaseLineage.BookshelfNG);
                        var databases = GetPostgresBackupDatabases();
                        if (!_startupContext.Args.TryGetValue("bookshelf-postgres-backup", out var backupPath) ||
                            string.IsNullOrWhiteSpace(backupPath))
                        {
                            throw new InvalidOperationException("PostgreSQL backup requires -bookshelf-postgres-backup=/path/to/backup-directory.");
                        }

                        var backup = new BookshelfPostgresExternalBackup(new ExternalToolRunner());
                        backup.VerifyTools();
                        backupPath = Path.GetFullPath(backupPath);
                        foreach (var (name, database) in databases)
                        {
                            backup.CreateDump(database.ConnectionString, Path.Combine(backupPath, $"postgres-{name}.dump"));
                            Console.WriteLine($"Independent PostgreSQL {name} database backup created and verified.");
                        }

                        break;
                    }

                case ApplicationModes.RestoreBookshelfPostgres:
                    {
                        if (!_startupContext.ConfirmBookshelfServiceStopped)
                        {
                            throw new InvalidOperationException("Restoring an independent PostgreSQL backup requires -confirm-bookshelf-service-stopped. Stop every process using this database first.");
                        }

                        GetPostgresBookshelfDatabase(DatabaseLineage.BookshelfNG, DatabaseLineage.Chaptarr);
                        var databases = GetPostgresBackupDatabases();
                        if (!_startupContext.Args.TryGetValue("bookshelf-postgres-backup", out var backupPath) ||
                            string.IsNullOrWhiteSpace(backupPath))
                        {
                            throw new InvalidOperationException("PostgreSQL restore requires -bookshelf-postgres-backup=/path/to/backup-directory.");
                        }

                        var backup = new BookshelfPostgresExternalBackup(new ExternalToolRunner());
                        backup.VerifyTools();
                        backupPath = Path.GetFullPath(backupPath);
                        var restoreItems = new System.Collections.Generic.List<(string Name, DatabaseConnectionInfo Database, string Path)>();
                        foreach (var (name, database) in databases)
                        {
                            backup.EnsureSourceDatabaseIsIdle(database.ConnectionString);
                            var dumpPath = Path.Combine(backupPath, $"postgres-{name}.dump");
                            backup.ValidateDump(dumpPath);
                            restoreItems.Add((name, database, dumpPath));
                        }

                        foreach (var (name, database, dumpPath) in restoreItems)
                        {
                            backup.RestoreDump(database.ConnectionString, dumpPath);
                            Console.WriteLine($"Independent PostgreSQL {name} database backup restored.");
                        }

                        break;
                    }

                default:
                    {
                        _consoleService.PrintHelp();
                        break;
                    }
            }
        }

        private DatabaseConnectionInfo GetPostgresBookshelfDatabase(params DatabaseLineage[] allowedLineages)
        {
            var database = _connectionStringFactory.MainDbConnection;
            if (database.DatabaseType != DatabaseType.PostgreSQL)
            {
                throw new InvalidDataException("The configured main database is not PostgreSQL.");
            }

            var lineage = DatabaseLineageGuard.Detect(database.ConnectionString, DatabaseType.PostgreSQL);
            if (!allowedLineages.Contains(lineage))
            {
                throw new InvalidDataException($"The configured PostgreSQL main database has unsupported lineage '{lineage}'.");
            }

            return database;
        }

        private (string Name, DatabaseConnectionInfo Database)[] GetPostgresBackupDatabases()
        {
            var databases = new[]
            {
                ("main", _connectionStringFactory.MainDbConnection),
                ("log", _connectionStringFactory.LogDbConnection),
                ("cache", _connectionStringFactory.CacheDbConnection)
            };

            if (databases.Any(database => database.Item2.DatabaseType != DatabaseType.PostgreSQL))
            {
                throw new InvalidDataException("BookshelfNG PostgreSQL backup requires PostgreSQL for its main, log, and cache databases.");
            }

            return databases
                .GroupBy(database => database.Item2.ConnectionString, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }
    }
}
