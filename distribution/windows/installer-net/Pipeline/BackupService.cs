using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;

namespace ChaptarrNG.Installer.Pipeline;

public sealed record BackupResult(string Method, string Path);

public static class BackupService
{
    public static string GetBackupRoot(string dataDirectory)
    {
        var parent = Directory.GetParent(dataDirectory)?.FullName
            ?? throw new InvalidOperationException($"Could not determine a backup location next to {dataDirectory}.");
        var leaf = Path.GetFileName(dataDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return Path.Combine(parent, leaf + ".backups", "ChaptarrNG-Migration");
    }

    public static string? GetUnsupportedSourceReason(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            return $"The BookshelfNG data directory does not exist: {dataDirectory}";
        }

        var serviceAccount = ServiceLifecycle.GetBookshelfServiceAccount();
        if (!string.Equals(serviceAccount, @"NT AUTHORITY\LocalService", StringComparison.OrdinalIgnoreCase))
        {
            return $"This installer supports the standard BookshelfNG LocalService account only. The Readarr service uses '{serviceAccount}'; use the manual migration path so its service identity and media permissions can be preserved.";
        }

        var serviceStartType = ServiceLifecycle.GetBookshelfServiceStartType();
        if (serviceStartType is not (2 or 3 or 4))
        {
            return $"The Readarr service uses unsupported Windows start type {serviceStartType}; use the manual migration path so its service startup policy can be preserved.";
        }

        var configPath = Path.Combine(dataDirectory, "config.xml");
        var databasePath = Path.Combine(dataDirectory, "readarr.db");
        if (!File.Exists(configPath))
        {
            return $"The BookshelfNG config file was not found: {configPath}";
        }

        string? postgresHost;
        try
        {
            var config = XDocument.Load(configPath);
            postgresHost = config
                .Descendants()
                .FirstOrDefault(e => string.Equals(e.Name.LocalName, "PostgresHost", StringComparison.OrdinalIgnoreCase))
                ?.Value;
        }
        catch (Exception ex)
        {
            return $"The BookshelfNG config could not be read safely: {ex.Message}";
        }

        var serviceEnvironmentConflict = GetServicePostgresEnvironmentConflict();
        if (serviceEnvironmentConflict is not null)
        {
            return serviceEnvironmentConflict;
        }

        var environmentSelectsPostgres = new[]
        {
            "CHAPTARR__POSTGRES__HOST",
            "READARR__POSTGRES__HOST",
            "AUDIOARR__POSTGRES__HOST"
        }.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

        var postgresSource = !string.IsNullOrWhiteSpace(postgresHost) || environmentSelectsPostgres;
        if (!postgresSource && !File.Exists(databasePath))
        {
            return "Neither a BookshelfNG SQLite database nor PostgreSQL configuration was found in this data directory.";
        }

        return null;
    }

    public static bool IsPostgresSource(string dataDirectory)
    {
        var configPath = Path.Combine(dataDirectory, "config.xml");
        if (File.Exists(configPath))
        {
            try
            {
                var config = XDocument.Load(configPath);
                var host = config
                    .Descendants()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, "PostgresHost", StringComparison.OrdinalIgnoreCase))
                    ?.Value;
                if (!string.IsNullOrWhiteSpace(host))
                {
                    return true;
                }
            }
            catch
            {
                // The preflight reports malformed config before the installer proceeds.
            }
        }

        return new[]
        {
            "CHAPTARR__POSTGRES__HOST",
            "READARR__POSTGRES__HOST",
            "AUDIOARR__POSTGRES__HOST"
        }.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));
    }

    private static string? GetServicePostgresEnvironmentConflict()
    {
        foreach (var item in ServiceLifecycle.GetBookshelfServiceEnvironment())
        {
            if (!IsPostgresEnvironmentVariable(item.Key))
            {
                continue;
            }

            var installerValue = Environment.GetEnvironmentVariable(item.Key);
            if (!string.Equals(item.Value, installerValue, StringComparison.Ordinal))
            {
                return $"The Readarr service has a service-specific PostgreSQL environment setting ({item.Key}) that the installer process and Chaptarr service cannot safely inherit. Move PostgreSQL settings to config.xml or machine-level environment variables before retrying.";
            }
        }

        return null;
    }

    private static bool IsPostgresEnvironmentVariable(string name)
    {
        return name.StartsWith("CHAPTARR__POSTGRES__", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("READARR__POSTGRES__", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("AUDIOARR__POSTGRES__", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<BackupResult> BackupAsync(
        string dataDirectory,
        IProgress<LogLine> log,
        bool postgresSource,
        Func<string, Task>? createPostgresDump,
        CancellationToken ct = default)
    {
        var backupRoot = GetBackupRoot(dataDirectory);
        Directory.CreateDirectory(backupRoot);
        SecureBackupDirectory(backupRoot);
        var target = Path.Combine(
            backupRoot,
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8]);

        log.Report(new LogLine(LogStream.Stdout, $"[backup] Target: {target}"));
        Directory.CreateDirectory(target);

        try
        {
            if (postgresSource)
            {
                CopySqliteSetIfPresent(dataDirectory, target, log);
                if (createPostgresDump is null)
                {
                    throw new InvalidOperationException("A PostgreSQL database backup callback was not provided.");
                }

                await createPostgresDump(target).ConfigureAwait(false);
                var mainDumpPath = Path.Combine(target, "postgres-main.dump");
                if (!File.Exists(mainDumpPath) || new FileInfo(mainDumpPath).Length == 0)
                {
                    throw new InvalidDataException("The independent PostgreSQL main-database backup was not created or was empty.");
                }

                log.Report(new LogLine(LogStream.Stdout, "[backup] Independent PostgreSQL database backup set created and verified."));
            }
            else
            {
                await CopyBinAsync(dataDirectory, target, log, ct).ConfigureAwait(false);
                CopySqliteSet(dataDirectory, target, log);
            }

            if (postgresSource)
            {
                await CopyBinAsync(dataDirectory, target, log, ct).ConfigureAwait(false);
            }

            CopySingleFile(Path.Combine(dataDirectory, "config.xml"), target, log);
            CopyBackupsFolder(dataDirectory, target, log);
            if (!postgresSource)
            {
                RunIntegrityCheck(target, log);
            }

            log.Report(new LogLine(LogStream.Stdout, $"[backup] Complete and retained: {target}"));
            return new BackupResult(postgresSource ? "PostgreSQLDump" : "FileCopy", target);
        }
        catch
        {
            try
            {
                Directory.Delete(target, recursive: true);
            }
            catch (Exception ex)
            {
                log.Report(new LogLine(LogStream.Stderr, $"[backup] Could not remove incomplete backup {target}: {ex.Message}"));
            }

            throw;
        }
    }

    private static void SecureBackupDirectory(string backupRoot)
    {
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        var inherited = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inherited,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            inherited,
            PropagationFlags.None,
            AccessControlType.Allow));
        new DirectoryInfo(backupRoot).SetAccessControl(security);
    }

    public static string GetDatabaseFingerprint(string dataDirectory)
    {
        return SqliteDatabaseFingerprint.Compute(dataDirectory);
    }

    public static string? GetPreviousVersion(string dataDirectory)
    {
        var exe = Path.Combine(dataDirectory, "bin", "Readarr.exe");
        if (!File.Exists(exe))
        {
            exe = Path.Combine(dataDirectory, "bin", "Readarr.Console.exe");
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

    private static async Task CopyBinAsync(
        string dataDirectory,
        string target,
        IProgress<LogLine> log,
        CancellationToken ct)
    {
        var source = Path.Combine(dataDirectory, "bin");
        if (!Directory.Exists(source))
        {
            throw new InvalidOperationException($"The BookshelfNG binary directory was not found: {source}");
        }

        var fileCount = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).Count();
        var sizeBytes = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Sum(f => new FileInfo(f).Length);
        var sizeMb = Math.Round(sizeBytes / 1024.0 / 1024.0);
        log.Report(new LogLine(LogStream.Stdout, $"[backup] Copying BookshelfNG binaries ({fileCount} files, ~{sizeMb:F0} MB)..."));

        var destination = Path.Combine(target, "bin");
        var exit = await ProcessStreamer.RunAsync(
            "robocopy.exe",
            new[] { source, destination, "/E", "/COPY:DAT", "/DCOPY:DAT", "/MT:8", "/NFL", "/NDL", "/NC", "/NS", "/NP" },
            null,
            log,
            ct).ConfigureAwait(false);

        // robocopy exit codes 0-7 are success bit flags; 8+ indicate failure.
        if (exit >= 8)
        {
            throw new InvalidOperationException($"robocopy failed with exit code {exit}; the upgrade was not applied.");
        }
    }

    private static void CopySqliteSet(string dataDirectory, string target, IProgress<LogLine> log)
    {
        var names = new[] { "readarr.db", "readarr.db-journal", "readarr.db-wal" };
        var primary = Path.Combine(dataDirectory, "readarr.db");
        var dbMb = Math.Round(new FileInfo(primary).Length / 1024.0 / 1024.0);
        log.Report(new LogLine(LogStream.Stdout, $"[backup] Copying SQLite database, WAL, and rollback journal ({dbMb:F0} MB)..."));

        foreach (var name in names)
        {
            var source = Path.Combine(dataDirectory, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(target, name), overwrite: false);
            }
        }
    }

    private static void CopySqliteSetIfPresent(string dataDirectory, string target, IProgress<LogLine> log)
    {
        var names = new[] { "readarr.db", "readarr.db-journal", "readarr.db-wal" };
        foreach (var name in names)
        {
            var source = Path.Combine(dataDirectory, name);
            if (File.Exists(source))
            {
                File.Copy(source, Path.Combine(target, name), overwrite: false);
                log.Report(new LogLine(LogStream.Stdout, $"[backup] Preserved coexisting file {name}."));
            }
        }
    }

    private static void CopySingleFile(string source, string targetDirectory, IProgress<LogLine> log)
    {
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("BookshelfNG config.xml disappeared before the backup completed.", source);
        }

        File.Copy(source, Path.Combine(targetDirectory, Path.GetFileName(source)), overwrite: false);
        log.Report(new LogLine(LogStream.Stdout, "[backup] Copied config.xml."));
    }

    private static void CopyBackupsFolder(string dataDirectory, string target, IProgress<LogLine> log)
    {
        var source = Path.Combine(dataDirectory, "Backups");
        if (!Directory.Exists(source))
        {
            return;
        }

        log.Report(new LogLine(LogStream.Stdout, "[backup] Copying existing BookshelfNG backups..."));
        CopyDirectoryRecursive(source, Path.Combine(target, "Backups"));
    }

    private static void CopyDirectoryRecursive(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectoryRecursive(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static void RunIntegrityCheck(string target, IProgress<LogLine> log)
    {
        var databasePath = Path.Combine(target, "readarr.db");
        log.Report(new LogLine(LogStream.Stdout, "[backup] Running SQLite PRAGMA integrity_check on the copy..."));

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        }.ToString();

        try
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            using var reader = command.ExecuteReader();

            var rows = new List<string>();
            while (reader.Read())
            {
                rows.Add(reader.GetString(0));
            }

            if (rows.Count == 1 && string.Equals(rows[0], "ok", StringComparison.OrdinalIgnoreCase))
            {
                log.Report(new LogLine(LogStream.Stdout, "[backup] integrity_check: ok"));
                return;
            }

            var result = rows.Count == 0 ? "(no output)" : string.Join("; ", rows);
            log.Report(new LogLine(LogStream.Stderr, $"[backup] integrity_check failed: {result}"));
            throw new InvalidOperationException($"SQLite integrity_check failed on the backup copy: {result}");
        }
        catch (SqliteException ex)
        {
            throw new InvalidOperationException($"SQLite integrity check error: {ex.Message}", ex);
        }
    }
}
