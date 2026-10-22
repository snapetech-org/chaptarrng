using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ChaptarrNG.Installer.Pipeline;

public sealed record InstallOutcome(bool Success, string? BackupPath, string? ErrorMessage);

public static class InstallPipeline
{
    private static readonly TimeSpan StopServiceTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan VerifyRunningTimeout = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VerifyHealthTimeout = TimeSpan.FromSeconds(45);

    public static async Task<InstallOutcome> RunUpgradeAsync(
        IUiReporter ui,
        CancellationToken ct = default)
    {
        var log = new Progress<LogLine>(line => ui.Log(line));
        var extractProgress = new Progress<ExtractProgress>(p =>
            ui.SetProgressPercent(p.Total == 0 ? 0 : p.Extracted * 100.0 / p.Total));
        string? dataDirectory = null;
        string? backupPath = null;
        string? stagingDirectory = null;
        string? previousBinDirectory = null;
        var serviceStopped = false;
        var bookshelfWasRunning = false;
        var postgresDatabaseMutationMayHaveStarted = false;
        int? bookshelfServiceStartType = null;

        try
        {
            ui.SetStep("Checking the existing BookshelfNG installation...");
            ui.SetProgressIndeterminate();

            if (!PayloadExtractor.HasEmbeddedPayload())
            {
                return Failure("This installer package does not contain the ChaptarrNG application payload.", null);
            }

            if (!ServiceLifecycle.IsInstalled(ServiceLifecycle.BookshelfServiceName))
            {
                return Failure("No Readarr service was found. This installer only migrates an existing BookshelfNG Windows service.", null);
            }

            if (ServiceLifecycle.IsInstalled(ServiceLifecycle.ChaptarrServiceName))
            {
                return Failure("A Chaptarr service is already installed. Stop and remove it using its own installer before migrating BookshelfNG.", null);
            }

            dataDirectory = ServiceLifecycle.GetBookshelfDataDirectory();
            var unsupportedSource = BackupService.GetUnsupportedSourceReason(dataDirectory);
            if (unsupportedSource is not null)
            {
                return Failure(unsupportedSource, null);
            }
            var postgresSource = BackupService.IsPostgresSource(dataDirectory);

            var providerMode = ui.ChooseProviderIdentityMode();
            if (!providerMode.HasValue)
            {
                return Failure("Migration cancelled before the BookshelfNG service was stopped.", null);
            }

            ui.Log($"[pipeline] BookshelfNG service data directory: {dataDirectory}");
            ui.Log($"[pipeline] Provider identity mode selected: {providerMode.Value}");
            if (postgresSource)
            {
                ui.SetStep("Checking PostgreSQL backup support...", "This handoff needs pg_dump, pg_restore, an explicit PostgresUser, and a reachable database for its independent rollback copy.");
                stagingDirectory = await PayloadExtractor.ExtractToStagingDirectoryAsync(
                    dataDirectory,
                    log,
                    extractProgress,
                    ct).ConfigureAwait(false);
                var checkExit = await ProcessStreamer.RunAsync(
                    Path.Combine(stagingDirectory, "Chaptarr.Console.exe"),
                    new[] { "-check-bookshelf-postgres-backup", $"-data={dataDirectory}" },
                    stagingDirectory,
                    log,
                    ct).ConfigureAwait(false);
                if (checkExit != 0)
                {
                    return Failure($"PostgreSQL backup preflight exited with code {checkExit}. Review the installer log, install pg_dump and pg_restore on PATH, and ensure PostgresUser is explicitly configured.", null);
                }
            }

            var initialServiceStatus = ServiceLifecycle.TryGetStatus(ServiceLifecycle.BookshelfServiceName);
            if (initialServiceStatus is not (ServiceControllerStatus.Running or ServiceControllerStatus.Stopped))
            {
                return Failure("The Readarr service is changing state; wait for it to reach RUNNING or STOPPED before migrating.", null);
            }
            bookshelfWasRunning = initialServiceStatus == ServiceControllerStatus.Running;
            bookshelfServiceStartType = ServiceLifecycle.GetBookshelfServiceStartType();
            ui.SetStep("Stopping BookshelfNG...", "The service must be stopped before the database backup and migration.");
            ct.ThrowIfCancellationRequested();
            if (!await ServiceLifecycle.StopAsync(
                    ServiceLifecycle.BookshelfServiceName,
                    log,
                    StopServiceTimeout,
                    CancellationToken.None).ConfigureAwait(false))
            {
                return Failure("Could not stop the BookshelfNG service. No migration was applied.", null);
            }

            serviceStopped = true;
            if (postgresSource)
            {
                ui.SetStep("Checking PostgreSQL connections...", "Stop every other application or client using the BookshelfNG database before the independent backup.");
                var idleCheckExit = await ProcessStreamer.RunAsync(
                    Path.Combine(stagingDirectory!, "Chaptarr.Console.exe"),
                    new[]
                    {
                        "-check-bookshelf-postgres-backup",
                        "-confirm-bookshelf-service-stopped",
                        $"-data={dataDirectory}"
                    },
                    stagingDirectory,
                    log,
                    CancellationToken.None).ConfigureAwait(false);
                if (idleCheckExit != 0)
                {
                    throw new InvalidOperationException($"PostgreSQL connection preflight exited with code {idleCheckExit}; the source database was not migrated.");
                }
            }

            ui.SetStep("Backing up BookshelfNG...", postgresSource
                ? "The original binaries, config, independent PostgreSQL dump, and existing backups are retained."
                : "The original binaries, config, SQLite database, and existing backups are retained.");
            var migrationConsole = stagingDirectory is null
                ? null
                : Path.Combine(stagingDirectory, "Chaptarr.Console.exe");
            var backup = await BackupService.BackupAsync(
                dataDirectory,
                log,
                postgresSource,
                postgresSource
                    ? async backupSetDirectory =>
                    {
                        var backupExit = await ProcessStreamer.RunAsync(
                            migrationConsole!,
                            new[]
                            {
                                "-backup-bookshelf-postgres",
                                "-confirm-bookshelf-service-stopped",
                                $"-bookshelf-postgres-backup={backupSetDirectory}",
                                $"-data={dataDirectory}"
                            },
                            stagingDirectory,
                            log,
                            ct).ConfigureAwait(false);
                        if (backupExit != 0)
                        {
                            throw new InvalidOperationException($"Independent PostgreSQL backup exited with code {backupExit}.");
                        }
                    }
                    : null,
                ct).ConfigureAwait(false);
            backupPath = backup.Path;
            RegistryService.WriteUpgradeStart(
                backup.Path,
                dataDirectory,
                providerMode.Value.ToString(),
                BackupService.GetPreviousVersion(dataDirectory),
                bookshelfServiceStartType.Value,
                bookshelfWasRunning);
            ui.Log($"[pipeline] Verified rollback backup recorded at {backup.Path}");

            if (!await ServiceLifecycle.SetStartTypeAsync(
                    ServiceLifecycle.BookshelfServiceName,
                    "disabled",
                    log,
                    ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The stopped BookshelfNG service could not be disabled during migration.");
            }

            stagingDirectory ??= await PayloadExtractor.ExtractToStagingDirectoryAsync(
                dataDirectory,
                log,
                extractProgress,
                ct).ConfigureAwait(false);
            var chaptarrConsole = Path.Combine(stagingDirectory, "Chaptarr.Console.exe");
            var environment = GetProviderEnvironment(providerMode.Value);
            ui.SetStep("Previewing the migration...", postgresSource
                ? "Keep every other PostgreSQL client stopped through review and apply. The independent database dump is retained for rollback."
                : "The SQLite source database is checked for committed changes through operator confirmation.");
            SqliteDatabaseFingerprint.Snapshot? sourceFingerprint = null;
            if (postgresSource)
            {
                var previewExit = await ProcessStreamer.RunAsync(
                    chaptarrConsole,
                    new[] { "-preview-bookshelf-migration", $"-data={dataDirectory}" },
                    stagingDirectory,
                    log,
                    ct,
                    environment).ConfigureAwait(false);
                if (previewExit != 0)
                {
                    throw new InvalidOperationException($"The read-only BookshelfNG migration preview exited with code {previewExit}.");
                }

                ui.SetStep("Review the migration preview...", "Read the row report and warnings in the log before continuing.");
                if (!ui.ConfirmMigrationApply(providerMode.Value, backup.Path))
                {
                    throw new OperationCanceledException("Migration cancelled at the preview review step.", ct);
                }

                var idleCheckExit = await ProcessStreamer.RunAsync(
                    chaptarrConsole,
                    new[]
                    {
                        "-check-bookshelf-postgres-backup",
                        "-confirm-bookshelf-service-stopped",
                        $"-data={dataDirectory}"
                    },
                    stagingDirectory,
                    log,
                    CancellationToken.None).ConfigureAwait(false);
                if (idleCheckExit != 0)
                {
                    throw new InvalidOperationException($"PostgreSQL connection preflight exited with code {idleCheckExit}; the source database was not migrated.");
                }
            }
            else
            {
                sourceFingerprint = SqliteDatabaseFingerprint.Capture(dataDirectory);
                using var sourceVersionGuard = new SqliteDatabaseVersionGuard(dataDirectory);
                var previewExit = await ProcessStreamer.RunAsync(
                    chaptarrConsole,
                    new[] { "-preview-bookshelf-migration", $"-data={dataDirectory}" },
                    stagingDirectory,
                    log,
                    ct,
                    environment).ConfigureAwait(false);
                if (previewExit != 0)
                {
                    throw new InvalidOperationException($"The read-only BookshelfNG migration preview exited with code {previewExit}.");
                }

                EnsureSourceDatabaseUnchanged(sourceVersionGuard, "during preview");

                ui.SetStep("Review the migration preview...", "Read the row report and warnings in the log before continuing.");
                if (!ui.ConfirmMigrationApply(providerMode.Value, backup.Path))
                {
                    throw new OperationCanceledException("Migration cancelled at the preview review step.", ct);
                }

                EnsureSourceDatabaseUnchanged(sourceVersionGuard, "while the preview awaited confirmation");
            }

            if (sourceFingerprint is not null)
            {
                var afterReviewFingerprint = SqliteDatabaseFingerprint.Capture(dataDirectory);
                LogSourceDatabaseRepresentationChange(sourceFingerprint, afterReviewFingerprint, "during preview and operator review", ui);
            }

            ui.SetStep("Applying BookshelfNG data migration...", "This modifies the database after the separate, verified backup.");
            var applyExit = await ProcessStreamer.RunAsync(
                chaptarrConsole,
                new[]
                {
                    "-migrate-bookshelf",
                    "-confirm-bookshelf-service-stopped",
                    $"-data={dataDirectory}"
                },
                stagingDirectory,
                log,
                ct,
                environment,
                () => postgresDatabaseMutationMayHaveStarted = postgresSource).ConfigureAwait(false);
            if (applyExit != 0)
            {
                throw new InvalidOperationException($"The BookshelfNG data migration exited with code {applyExit}.");
            }

            ui.SetStep("Switching the Windows service to ChaptarrNG...");
            previousBinDirectory = PayloadExtractor.ReplaceBinDirectory(dataDirectory, stagingDirectory, log);
            stagingDirectory = null;

            var installedConsole = Path.Combine(dataDirectory, "bin", "Chaptarr.Console.exe");
            if (!File.Exists(installedConsole))
            {
                throw new FileNotFoundException("The ChaptarrNG service executable is missing after the binary switch.", installedConsole);
            }

            ui.SetStep("Registering the ChaptarrNG service...", "The Readarr data directory is explicitly preserved in the service command.");
            var installExit = await ProcessStreamer.RunAsync(
                installedConsole,
                new[] { "/i", $"-data={dataDirectory}", "/nobrowser" },
                Path.GetDirectoryName(installedConsole),
                log,
                ct).ConfigureAwait(false);
            if (installExit != 0)
            {
                throw new InvalidOperationException($"Chaptarr.Console.exe service registration exited with code {installExit}.");
            }

            ui.SetStep("Verifying ChaptarrNG...", "Waiting for the service and local health endpoint.");
            if (!await ServiceLifecycle.PollForRunningAsync(
                    ServiceLifecycle.ChaptarrServiceName,
                    log,
                    VerifyRunningTimeout,
                    ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The ChaptarrNG service did not reach RUNNING.");
            }

            var chaptarrImagePath = ServiceLifecycle.GetImagePath(ServiceLifecycle.ChaptarrServiceName);
            if (string.IsNullOrWhiteSpace(chaptarrImagePath) ||
                !chaptarrImagePath.Contains(dataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The ChaptarrNG service command does not preserve the BookshelfNG data directory.");
            }

            if (!await WaitForHealthAsync(dataDirectory, VerifyHealthTimeout, log, ct).ConfigureAwait(false))
            {
                throw new InvalidOperationException("The ChaptarrNG service is running but did not pass its local /ping health check.");
            }

            var newVersion = ReadInstalledChaptarrVersion(dataDirectory);
            if (!string.IsNullOrWhiteSpace(newVersion))
            {
                try
                {
                    RegistryService.WriteCurrentVersion(newVersion);
                }
                catch (Exception ex)
                {
                    ui.LogError($"[pipeline] Could not record ChaptarrNG version in the installer registry: {ex.Message}");
                }
            }

            var serviceCleanupScheduled = ServiceCleanupRunner.ScheduleLegacyServiceCleanup(dataDirectory, ui);
            if (!serviceCleanupScheduled)
            {
                ui.LogError("[installer] The stopped Readarr service remains disabled so it cannot start beside ChaptarrNG. The installer could not schedule its removal after this window closes; restore remains available.");
            }

            PayloadExtractor.TryDeleteDirectory(previousBinDirectory);
            serviceStopped = false;
            ui.SetProgressPercent(100);
            ui.SetStep(
                "Migration complete.",
                $"ChaptarrNG is running with the BookshelfNG data directory. The verified rollback backup is retained at {backup.Path}. " +
                (serviceCleanupScheduled
                    ? "The disabled Readarr service will be removed after this installer closes."
                    : "Readarr remains disabled; use Restore BookshelfNG if you need to revert."));
            return new InstallOutcome(true, backup.Path, null);
        }
        catch (OperationCanceledException)
        {
            return await RestoreAfterFailureAsync(ui, log, dataDirectory, backupPath, previousBinDirectory, stagingDirectory, serviceStopped, bookshelfWasRunning, bookshelfServiceStartType, postgresDatabaseMutationMayHaveStarted, "Migration cancelled.")
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            ui.LogError($"[pipeline] {ex.GetType().Name}: {ex.Message}");
            return await RestoreAfterFailureAsync(ui, log, dataDirectory, backupPath, previousBinDirectory, stagingDirectory, serviceStopped, bookshelfWasRunning, bookshelfServiceStartType, postgresDatabaseMutationMayHaveStarted, ex.Message)
                .ConfigureAwait(false);
        }
        finally
        {
            if (stagingDirectory is not null)
            {
                PayloadExtractor.TryDeleteDirectory(stagingDirectory);
            }
        }
    }

    public static async Task<InstallOutcome> RunRestoreAsync(IUiReporter ui, CancellationToken ct = default)
    {
        var log = new Progress<LogLine>(line => ui.Log(line));
        try
        {
            var record = RegistryService.Read();
            if (record?.BackupPath is null ||
                string.IsNullOrWhiteSpace(record.DataDirectory) ||
                !RegistryService.HasRecoverableUpgrade())
            {
                return Failure("No complete BookshelfNG migration backup was found in the installer registry record.", null);
            }
            if (ct.IsCancellationRequested)
            {
                return Failure("Restore was cancelled before it started.", record.BackupPath);
            }

            ui.SetStep("Restoring BookshelfNG from the migration backup...", record.BackupPath);
            ui.SetProgressIndeterminate();
            var exit = await RollbackRunner.RunAsync(
                record.BackupPath,
                record.DataDirectory,
                log,
                CancellationToken.None,
                record.BookshelfServiceStartType,
                record.BookshelfServiceWasRunning,
                Path.Combine(record.DataDirectory, "bin", "Chaptarr.Console.exe"),
                restorePostgresDatabase: File.Exists(Path.Combine(record.BackupPath, "postgres-main.dump"))).ConfigureAwait(false);
            if (exit != 0)
            {
                return Failure($"Rollback failed with exit code {exit}. The backup is still available at {record.BackupPath}.", record.BackupPath);
            }

            RecordRollbackTimestamp(ui);
            ui.SetProgressPercent(100);
            ui.SetStep("BookshelfNG restored.", "The original Readarr service and database backup are in use again.");
            return new InstallOutcome(true, record.BackupPath, null);
        }
        catch (Exception ex)
        {
            ui.LogError($"[rollback] {ex.GetType().Name}: {ex.Message}");
            return Failure(ex.Message, null);
        }
    }

    private static async Task<InstallOutcome> RestoreAfterFailureAsync(
        IUiReporter ui,
        IProgress<LogLine> log,
        string? dataDirectory,
        string? backupPath,
        string? previousBinDirectory,
        string? stagingDirectory,
        bool serviceStopped,
        bool bookshelfWasRunning,
        int? bookshelfServiceStartType,
        bool restorePostgresDatabase,
        string reason)
    {
        ui.LogError($"[pipeline] {reason}");
        if (dataDirectory is null || !serviceStopped)
        {
            return Failure(reason, backupPath);
        }

        ui.SetStep("Restoring BookshelfNG...", backupPath is null
            ? "No complete migration backup exists; restarting the original service."
            : $"Restoring binaries, database, and service from {backupPath}");

        if (backupPath is null)
        {
            var started = !bookshelfWasRunning || await ServiceLifecycle.StartAsync(
                    ServiceLifecycle.BookshelfServiceName,
                    log,
                    VerifyRunningTimeout,
                    CancellationToken.None).ConfigureAwait(false);
            return Failure(
                started
                    ? reason + (bookshelfWasRunning
                        ? " The original BookshelfNG service was restarted."
                        : " BookshelfNG was already stopped and remains stopped.")
                    : reason + " BookshelfNG did not restart; start Readarr manually and check the Windows Event Viewer.",
                null);
        }

        try
        {
            var exit = await RollbackRunner.RunAsync(
                backupPath,
                dataDirectory,
                log,
                CancellationToken.None,
                bookshelfServiceStartType,
                bookshelfWasRunning,
                ResolveMigrationConsole(dataDirectory, stagingDirectory),
                restorePostgresDatabase).ConfigureAwait(false);
            if (exit == 0)
            {
                RecordRollbackTimestamp(ui);
                if (previousBinDirectory is not null)
                {
                    PayloadExtractor.TryDeleteDirectory(previousBinDirectory);
                }

                return Failure(
                    reason + $"\n\nBookshelfNG was restored from the verified backup at:\n{backupPath}",
                    backupPath);
            }

            return Failure(
                reason + $"\n\nAutomatic rollback exited with code {exit}. The verified backup is preserved at:\n{backupPath}\nUse the installer’s Restore BookshelfNG action or the manual recovery steps.",
                backupPath);
        }
        catch (Exception rollbackException)
        {
            ui.LogError($"[rollback] {rollbackException.GetType().Name}: {rollbackException.Message}");
            return Failure(
                reason + $"\n\nAutomatic rollback failed. The verified backup is preserved at:\n{backupPath}\nRestore BookshelfNG manually before starting either service.",
                backupPath);
        }
    }

    private static System.Collections.Generic.IReadOnlyDictionary<string, string?> GetProviderEnvironment(
        ProviderIdentityMode mode)
    {
        return mode switch
        {
            ProviderIdentityMode.NativeHardcover => new System.Collections.Generic.Dictionary<string, string?>
            {
                ["HARDCOVER"] = "true",
                ["HARDCOVER_NATIVE"] = null
            },
            _ => new System.Collections.Generic.Dictionary<string, string?>
            {
                ["HARDCOVER"] = "false",
                ["HARDCOVER_NATIVE"] = "false"
            }
        };
    }

    private static async Task<bool> WaitForHealthAsync(
        string dataDirectory,
        TimeSpan timeout,
        IProgress<LogLine> log,
        CancellationToken ct)
    {
        var configPath = Path.Combine(dataDirectory, "config.xml");
        var config = XDocument.Load(configPath);
        string? GetValue(string name) => config
            .Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value;

        var enableSsl = bool.TryParse(GetValue("EnableSsl"), out var ssl) && ssl;
        var portText = GetValue(enableSsl ? "SslPort" : "Port");
        var port = int.TryParse(portText, out var configuredPort) && configuredPort is > 0 and <= 65535
            ? configuredPort
            : enableSsl ? 6868 : 8789;
        var bindAddress = GetValue("BindAddress");
        var host = string.IsNullOrWhiteSpace(bindAddress) || bindAddress is "*" or "+" or "0.0.0.0"
            ? "127.0.0.1"
            : bindAddress == "::" ? "::1" : bindAddress;
        var scheme = enableSsl ? "https" : "http";
        var uriHost = host.Contains(':') && !host.StartsWith("[", StringComparison.Ordinal) ? $"[{host}]" : host;
        var urlBase = GetValue("UrlBase")?.Trim('/');
        var pingPath = string.IsNullOrWhiteSpace(urlBase) ? "/ping" : $"/{urlBase}/ping";
        var uri = new Uri($"{scheme}://{uriHost}:{port}{pingPath}");
        var loopback = host is "127.0.0.1" or "::1" or "localhost";
        var handler = new HttpClientHandler();
        if (enableSsl && loopback)
        {
            handler.ServerCertificateCustomValidationCallback =
                (_, _, _, _) => true;
        }

        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };
        log.Report(new LogLine(LogStream.Stdout, $"[health] Checking {scheme}://{uriHost}:{port}/ping..."));
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var response = await client.GetAsync(uri, ct).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    log.Report(new LogLine(LogStream.Stdout, "[health] ChaptarrNG /ping returned success."));
                    return true;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // The service can report RUNNING before its HTTP listener is ready.
            }

            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        log.Report(new LogLine(LogStream.Stderr, $"[health] {uri} did not return success within {timeout.TotalSeconds:F0}s."));
        return false;
    }

    private static InstallOutcome Failure(string message, string? backupPath) =>
        new(false, backupPath, message);

    private static void RecordRollbackTimestamp(IUiReporter ui)
    {
        try
        {
            RegistryService.WriteRollbackTimestamp();
        }
        catch (Exception ex)
        {
            ui.LogError($"[rollback] Could not record the rollback timestamp: {ex.Message}");
        }
    }

    private static void EnsureSourceDatabaseUnchanged(
        SqliteDatabaseVersionGuard sourceVersionGuard,
        string phase)
    {
        if (sourceVersionGuard.HasChanged)
        {
            throw new InvalidDataException($"BookshelfNG's SQLite database received a committed change {phase}. The installer stopped before apply and will restore the verified backup.");
        }
    }

    private static void LogSourceDatabaseRepresentationChange(
        SqliteDatabaseFingerprint.Snapshot initialFingerprint,
        SqliteDatabaseFingerprint.Snapshot currentFingerprint,
        string phase,
        IUiReporter ui)
    {
        var changedFiles = SqliteDatabaseFingerprint.GetChangedFiles(initialFingerprint, currentFingerprint);
        if (!string.Equals(initialFingerprint.Hash, currentFingerprint.Hash, StringComparison.Ordinal))
        {
            ui.Log($"[pipeline] SQLite file representation changed {phase} ({string.Join(", ", changedFiles)}), but PRAGMA data_version did not change; no committed source database change was detected.");
        }
    }

    private static string? ReadInstalledChaptarrVersion(string dataDirectory)
    {
        var exe = Path.Combine(dataDirectory, "bin", "Chaptarr.exe");
        if (!File.Exists(exe))
        {
            exe = Path.Combine(dataDirectory, "bin", "Chaptarr.Console.exe");
        }

        if (!File.Exists(exe))
        {
            return null;
        }

        try
        {
            return System.Diagnostics.FileVersionInfo.GetVersionInfo(exe).FileVersion;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveMigrationConsole(string dataDirectory, string? stagingDirectory)
    {
        var installed = Path.Combine(dataDirectory, "bin", "Chaptarr.Console.exe");
        if (File.Exists(installed))
        {
            return installed;
        }

        var staged = stagingDirectory is null ? null : Path.Combine(stagingDirectory, "Chaptarr.Console.exe");
        return staged is not null && File.Exists(staged) ? staged : null;
    }
}
