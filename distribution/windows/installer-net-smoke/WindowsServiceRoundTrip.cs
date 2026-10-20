using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Net.Sockets;
using System.Xml.Linq;
using ChaptarrNG.Installer.Pipeline;
using Microsoft.Data.Sqlite;
using Microsoft.Win32;

internal static class WindowsServiceRoundTrip
{
    private const string BookshelfArchiveUrl =
        "https://github.com/snapetech-org/bookshelfng/releases/download/main-v0.4.21.71/BookshelfNG-main-v0.4.21.71-win-x64.zip";
    private const string BookshelfArchiveSha256 =
        "54200ccd62c3f20d71c52144af0bbb38117150ab04c657263ffb9c7cf1391762";
    private const string BookshelfRegistryKey = @"SOFTWARE\ChaptarrNG\BookshelfMigration";
    private const string LocalService = @"NT AUTHORITY\LocalService";

    public static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL: Windows service round-trip smoke can only run on Windows.");
            return 1;
        }

        var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
        if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
        {
            Console.Error.WriteLine("FAIL: Windows service round-trip smoke requires an elevated process.");
            return 1;
        }

        if (ServiceLifecycle.IsInstalled(ServiceLifecycle.BookshelfServiceName) ||
            ServiceLifecycle.IsInstalled(ServiceLifecycle.ChaptarrServiceName))
        {
            Console.Error.WriteLine("FAIL: refusing to run while a Readarr or Chaptarr service already exists on this runner.");
            return 1;
        }

        if (RegistryService.Read() is not null)
        {
            Console.Error.WriteLine("FAIL: refusing to replace an existing Bookshelf migration registry record.");
            return 1;
        }

        var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        var scratchRoot = Path.Combine(
            string.IsNullOrWhiteSpace(runnerTemp) ? Path.GetTempPath() : runnerTemp,
            "chaptarr-bookshelf-service-roundtrip-" + Guid.NewGuid().ToString("N"));
        var dataDirectory = Path.Combine(scratchRoot, "Readarr Data With Spaces");
        var archivePath = Path.Combine(scratchRoot, "bookshelfng-win-x64.zip");
        var extractionDirectory = Path.Combine(scratchRoot, "bookshelfng");
        var installerTempDirectory = Path.Combine(Path.GetTempPath(), "ChaptarrNG.Installer");
        var installerTempDirectoryExisted = Directory.Exists(installerTempDirectory);
        var failed = false;
        var preserveScratch = false;

        try
        {
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(extractionDirectory);

            await DownloadBookshelfArchiveAsync(archivePath).ConfigureAwait(false);
            ExtractBookshelfArchive(archivePath, extractionDirectory);
            var bookshelfConsole = Directory
                .EnumerateFiles(extractionDirectory, "Readarr.Console.exe", SearchOption.AllDirectories)
                .SingleOrDefault()
                ?? throw new FileNotFoundException("The pinned BookshelfNG archive did not contain Readarr.Console.exe.");

            var sourceBin = Path.GetDirectoryName(bookshelfConsole)
                ?? throw new InvalidDataException("Could not determine the BookshelfNG binary directory.");
            var activeBin = Path.Combine(dataDirectory, "bin");
            CopyDirectory(sourceBin, activeBin);
            SeedBookshelfDatabase(dataDirectory);
            await GrantServicePermissionsAsync(activeBin, dataDirectory).ConfigureAwait(false);

            Console.WriteLine("[smoke] Registering and starting the pinned BookshelfNG 0.4.21.71 service...");
            await RunProcessAsync(
                Path.Combine(activeBin, "Readarr.Console.exe"),
                new[] { "/i", $"-data={dataDirectory}", "/nobrowser" }).ConfigureAwait(false);
            await EnsureLocalServiceAccountAsync(ServiceLifecycle.BookshelfServiceName).ConfigureAwait(false);
            await StartServiceIfStoppedAsync(ServiceLifecycle.BookshelfServiceName).ConfigureAwait(false);
            await WaitForServiceAsync(ServiceLifecycle.BookshelfServiceName, ServiceControllerStatus.Running, TimeSpan.FromSeconds(90))
                .ConfigureAwait(false);
            await WaitForHealthAsync(dataDirectory, TimeSpan.FromSeconds(90)).ConfigureAwait(false);

            var sourceDatabase = Path.Combine(dataDirectory, "readarr.db");
            AssertLineage(sourceDatabase, DatabaseLineageKind.BookshelfNG, "BookshelfNG source startup");
            AssertRowCount(sourceDatabase, "Books", expectedAtLeast: 1, "BookshelfNG source data");
            await StopServiceAsync(ServiceLifecycle.BookshelfServiceName).ConfigureAwait(false);

            Console.WriteLine("[smoke] Injecting an HTTP port conflict to exercise automatic installer rollback...");
            using (var blocker = new TcpListener(IPAddress.Loopback, GetConfiguredHttpPort(dataDirectory)))
            using (var releaseCancellation = new CancellationTokenSource())
            {
                blocker.Start();
                var releaseBlocker = ReleasePortBlockerAfterChaptarrStopsAsync(blocker, releaseCancellation.Token);
                InstallOutcome failedUpgrade;
                try
                {
                    failedUpgrade = await InstallPipeline.RunUpgradeAsync(new SmokeUiReporter()).ConfigureAwait(false);
                }
                finally
                {
                    blocker.Stop();
                    releaseCancellation.Cancel();
                    try { await releaseBlocker.ConfigureAwait(false); }
                    catch (OperationCanceledException) { }
                }

                if (failedUpgrade.Success ||
                    string.IsNullOrWhiteSpace(failedUpgrade.BackupPath) ||
                    string.IsNullOrWhiteSpace(RegistryService.Read()?.LastRollbackTime))
                {
                    throw new InvalidOperationException(
                        "The injected Chaptarr health-check failure did not complete automatic BookshelfNG rollback.");
                }

                var automaticRollbackBackup = Path.Combine(failedUpgrade.BackupPath, "readarr.db");
                AssertLineage(automaticRollbackBackup, DatabaseLineageKind.BookshelfNG, "automatic rollback backup");
                await WaitForServiceAsync(ServiceLifecycle.BookshelfServiceName, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30))
                    .ConfigureAwait(false);
                await WaitForHealthAsync(dataDirectory, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
                AssertServicePath(ServiceLifecycle.BookshelfServiceName, dataDirectory);
                AssertLineage(sourceDatabase, DatabaseLineageKind.BookshelfNG, "automatic rollback database");
                AssertRowCount(sourceDatabase, "Books", expectedAtLeast: 1, "automatically restored BookshelfNG books");
                AssertDatabaseIntegrity(sourceDatabase, "automatically restored BookshelfNG database");
            }

            Console.WriteLine("[smoke] Running the real installer upgrade pipeline with synthetic populated migration data...");
            var ui = new SmokeUiReporter();
            var upgrade = await InstallPipeline.RunUpgradeAsync(ui).ConfigureAwait(false);
            if (!upgrade.Success || string.IsNullOrWhiteSpace(upgrade.BackupPath))
            {
                throw new InvalidOperationException("The installer upgrade pipeline failed: " + upgrade.ErrorMessage);
            }

            var migrationBackupDatabase = Path.Combine(upgrade.BackupPath, "readarr.db");
            AssertLineage(migrationBackupDatabase, DatabaseLineageKind.BookshelfNG, "retained migration backup");
            AssertRowCount(migrationBackupDatabase, "Books", expectedAtLeast: 1, "retained BookshelfNG backup");
            AssertLineage(sourceDatabase, DatabaseLineageKind.Chaptarr, "migrated Chaptarr database");
            AssertRowCount(sourceDatabase, "Authors", expectedAtLeast: 1, "migrated authors");
            AssertRowCount(sourceDatabase, "Books", expectedAtLeast: 1, "migrated books");
            AssertRowCount(sourceDatabase, "Editions", expectedAtLeast: 2, "migrated editions");
            AssertRowCount(sourceDatabase, "BookFiles", expectedAtLeast: 2, "migrated audiobook and ebook files");
            AssertDatabaseIntegrity(sourceDatabase, "migrated Chaptarr database");
            AssertServicePath(ServiceLifecycle.ChaptarrServiceName, dataDirectory);
            await WaitForServiceAsync(ServiceLifecycle.ChaptarrServiceName, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);

            Console.WriteLine("[smoke] Running the installer restore action and its embedded rollback script...");
            var restore = await InstallPipeline.RunRestoreAsync(ui).ConfigureAwait(false);
            if (!restore.Success)
            {
                throw new InvalidOperationException("The installer restore pipeline failed: " + restore.ErrorMessage);
            }

            if (ServiceLifecycle.IsInstalled(ServiceLifecycle.ChaptarrServiceName))
            {
                throw new InvalidOperationException("The Chaptarr service remained registered after BookshelfNG restore.");
            }

            await WaitForServiceAsync(ServiceLifecycle.BookshelfServiceName, ServiceControllerStatus.Running, TimeSpan.FromSeconds(30))
                .ConfigureAwait(false);
            await WaitForHealthAsync(dataDirectory, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            AssertServicePath(ServiceLifecycle.BookshelfServiceName, dataDirectory);
            AssertServiceExecutablePath(
                ServiceLifecycle.BookshelfServiceName,
                Path.Combine(dataDirectory, "bin", "Readarr.Console.exe"));
            if (!string.Equals(ReadServiceAccount(ServiceLifecycle.BookshelfServiceName), LocalService, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The restored Readarr service did not preserve the supported LocalService account.");
            }
            AssertLineage(sourceDatabase, DatabaseLineageKind.BookshelfNG, "restored BookshelfNG database");
            AssertRowCount(sourceDatabase, "Books", expectedAtLeast: 1, "restored BookshelfNG books");
            AssertRowCount(sourceDatabase, "BookFiles", expectedAtLeast: 2, "restored BookshelfNG files");
            AssertDatabaseIntegrity(sourceDatabase, "restored BookshelfNG database");

            var registry = RegistryService.Read();
            if (string.IsNullOrWhiteSpace(registry?.LastRollbackTime) ||
                !string.Equals(Path.GetFullPath(registry.DataDirectory ?? ""), Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The installer did not record the completed rollback against the selected data directory.");
            }

            Console.WriteLine("PASS: BookshelfNG 0.4.21.71 upgraded through the installer, passed Chaptarr health checks, and restored through the embedded rollback script.");
        }
        catch (Exception exception)
        {
            failed = true;
            Console.Error.WriteLine($"FAIL: {exception.GetType().Name}: {exception.Message}");
        }

        var cleanupOk = await CleanupAsync(
            scratchRoot,
            installerTempDirectory,
            removeInstallerTempDirectory: !installerTempDirectoryExisted).ConfigureAwait(false);
        if (!cleanupOk)
        {
            preserveScratch = true;
            failed = true;
        }

        if (preserveScratch)
        {
            Console.Error.WriteLine($"Test services could not be removed safely. Keeping their files at {scratchRoot}.");
        }

        return failed ? 1 : 0;
    }

    private static async Task DownloadBookshelfArchiveAsync(string destination)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var response = await client.GetAsync(BookshelfArchiveUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        await using (var output = File.Create(destination))
        {
            await input.CopyToAsync(output).ConfigureAwait(false);
        }

        await using var archive = File.OpenRead(destination);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(archive).ConfigureAwait(false)).ToLowerInvariant();
        if (!string.Equals(actual, BookshelfArchiveSha256, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"The downloaded BookshelfNG archive checksum did not match the pinned release asset ({actual}).");
        }
    }

    private static void ExtractBookshelfArchive(string archivePath, string destination)
    {
        ZipFile.ExtractToDirectory(archivePath, destination);
    }

    private static void SeedBookshelfDatabase(string dataDirectory)
    {
        var fixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var schema = File.ReadAllText(Path.Combine(fixtureDirectory, "BookshelfNG.Schema.158.sql"));
        var populated = File.ReadAllText(Path.Combine(fixtureDirectory, "BookshelfNG.PopulatedMigration158.sql"));
        var databasePath = Path.Combine(dataDirectory, "readarr.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ConnectionString;

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = schema;
            schemaCommand.ExecuteNonQuery();
        }

        using (var populateCommand = connection.CreateCommand())
        {
            populateCommand.CommandText = populated + @"
                UPDATE Indexers SET EnableRss = 0, EnableAutomaticSearch = 0, EnableInteractiveSearch = 0;
                UPDATE ImportLists SET EnableAutomaticAdd = 0, ShouldSearch = 0, ShouldMonitorExisting = 0;
                UPDATE Authors SET Monitored = 0, LastInfoSync = CURRENT_TIMESTAMP;
                UPDATE Books SET Monitored = 0, LastInfoSync = CURRENT_TIMESTAMP;
            ";
            populateCommand.ExecuteNonQuery();
        }
    }

    private static async Task GrantServicePermissionsAsync(string binaryDirectory, string dataDirectory)
    {
        await RunProcessAsync("icacls.exe", new[]
        {
            binaryDirectory, "/grant", LocalService + ":(OI)(CI)RX", "/T", "/C"
        }).ConfigureAwait(false);
        await RunProcessAsync("icacls.exe", new[]
        {
            dataDirectory, "/grant", LocalService + ":(OI)(CI)M", "/T", "/C"
        }).ConfigureAwait(false);
    }

    private static async Task EnsureLocalServiceAccountAsync(string serviceName)
    {
        var account = serviceName == ServiceLifecycle.BookshelfServiceName
            ? ServiceLifecycle.GetBookshelfServiceAccount()
            : ReadServiceAccount(serviceName);
        if (string.Equals(account, LocalService, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await StopServiceAsync(serviceName).ConfigureAwait(false);
        await RunProcessAsync("sc.exe", new[] { "config", serviceName, "obj=", LocalService }).ConfigureAwait(false);
    }

    private static string? ReadServiceAccount(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}", writable: false);
        return key?.GetValue("ObjectName") as string;
    }

    private static async Task WaitForServiceAsync(
        string serviceName,
        ServiceControllerStatus expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var status = ServiceLifecycle.TryGetStatus(serviceName);
            if (status == expected)
            {
                return;
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new System.TimeoutException($"Windows service {serviceName} did not reach {expected} within {timeout.TotalSeconds:0} seconds. Current status: {ServiceLifecycle.TryGetStatus(serviceName)?.ToString() ?? "not installed"}.");
    }

    private static async Task StopServiceAsync(string serviceName)
    {
        var stopped = await ServiceLifecycle.StopAsync(
            serviceName,
            new Progress<LogLine>(line => Console.WriteLine(line.Text)),
            TimeSpan.FromSeconds(30)).ConfigureAwait(false);
        if (!stopped)
        {
            throw new InvalidOperationException($"The {serviceName} service and its process did not stop cleanly.");
        }
    }

    private static Task StartServiceIfStoppedAsync(string serviceName)
    {
        using var service = new ServiceController(serviceName);
        service.Refresh();
        if (service.Status == ServiceControllerStatus.Stopped)
        {
            service.Start();
        }

        return Task.CompletedTask;
    }

    private static async Task WaitForHealthAsync(string dataDirectory, TimeSpan timeout)
    {
        var configPath = Path.Combine(dataDirectory, "config.xml");
        var deadline = DateTime.UtcNow + timeout;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var config = XDocument.Load(configPath);
                var portText = config.Descendants().FirstOrDefault(e => e.Name.LocalName == "Port")?.Value;
                var port = int.TryParse(portText, out var parsed) ? parsed : 8787;
                using var response = await client.GetAsync($"http://127.0.0.1:{port}/ping").ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
            {
                // Service startup may report RUNNING before its HTTP listener is ready.
            }

            await Task.Delay(1000).ConfigureAwait(false);
        }

        throw new System.TimeoutException($"The service in {dataDirectory} did not pass its local /ping check.");
    }

    private static int GetConfiguredHttpPort(string dataDirectory)
    {
        var config = XDocument.Load(Path.Combine(dataDirectory, "config.xml"));
        var portText = config.Descendants().FirstOrDefault(e => e.Name.LocalName == "Port")?.Value;
        return int.TryParse(portText, out var port) && port is > 0 and <= 65535 ? port : 8787;
    }

    private static async Task ReleasePortBlockerAfterChaptarrStopsAsync(
        TcpListener blocker,
        CancellationToken cancellationToken)
    {
        var sawChaptarrRunning = false;
        while (!cancellationToken.IsCancellationRequested)
        {
            var chaptarrStatus = ServiceLifecycle.TryGetStatus(ServiceLifecycle.ChaptarrServiceName);
            if (chaptarrStatus == ServiceControllerStatus.Running)
            {
                sawChaptarrRunning = true;
            }

            if (sawChaptarrRunning &&
                (chaptarrStatus is null || chaptarrStatus == ServiceControllerStatus.Stopped))
            {
                Console.WriteLine("[smoke] Chaptarr has stopped; releasing the injected port conflict before BookshelfNG restore.");
                blocker.Stop();
                return;
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    private static void AssertLineage(string databasePath, DatabaseLineageKind expected, string label)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
            using var reader = tableCommand.ExecuteReader();
            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }
        }

        if (!tables.Contains("Authors"))
        {
            throw new InvalidDataException($"Expected {label} to contain Authors; found no supported database lineage.");
        }

        var authorColumns = GetColumns(connection, "Authors");
        var actual = DatabaseLineageKind.Unsupported;
        if (authorColumns.Contains("AudiobookRootFolderPath") && authorColumns.Contains("EbookRootFolderPath"))
        {
            actual = DatabaseLineageKind.Chaptarr;
        }
        else if (tables.Contains("AuthorMetadata") && tables.Contains("Books") &&
                 authorColumns.Contains("AuthorMetadataId") &&
                 GetColumns(connection, "Books").Contains("ForeignBookId"))
        {
            actual = DatabaseLineageKind.BookshelfNG;
        }

        if (actual != expected)
        {
            throw new InvalidDataException($"Expected {label} to use {expected} lineage, found {actual}.");
        }
    }

    private static HashSet<string> GetColumns(SqliteConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static void AssertRowCount(string databasePath, string table, long expectedAtLeast, string label)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{table}\";";
        var count = Convert.ToInt64(command.ExecuteScalar());
        if (count < expectedAtLeast)
        {
            throw new InvalidDataException($"Expected {label} to contain at least {expectedAtLeast} rows, found {count}.");
        }
    }

    private static void AssertDatabaseIntegrity(string databasePath, string label)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ConnectionString;
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using (var integrity = connection.CreateCommand())
        {
            integrity.CommandText = "PRAGMA integrity_check;";
            var result = integrity.ExecuteScalar()?.ToString();
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"SQLite integrity_check failed for {label}: {result ?? "no result"}.");
            }
        }

        using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            using var reader = foreignKeys.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidDataException($"SQLite foreign_key_check found a violation in {label}.");
            }
        }
    }

    private static void AssertServicePath(string serviceName, string dataDirectory)
    {
        var imagePath = ServiceLifecycle.GetImagePath(serviceName);
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !imagePath.Contains(dataDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"The {serviceName} service command did not retain {dataDirectory}.");
        }
    }

    private static void AssertServiceExecutablePath(string serviceName, string executablePath)
    {
        var imagePath = ServiceLifecycle.GetImagePath(serviceName);
        var expectedExecutable = "\"" + Path.GetFullPath(executablePath) + "\"";
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !imagePath.StartsWith(expectedExecutable, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {serviceName} service command must start with its quoted executable path {expectedExecutable}.");
        }
    }

    private static async Task RunProcessAsync(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var executableDirectory = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrWhiteSpace(executableDirectory))
        {
            startInfo.WorkingDirectory = executableDirectory;
        }
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {fileName}.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new System.TimeoutException($"{fileName} did not finish within three minutes.");
        }

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(stdout))
        {
            Console.WriteLine(stdout.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            Console.Error.WriteLine(stderr.TrimEnd());
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"{fileName} exited with code {process.ExitCode}.");
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: false);
        }
    }

    private static async Task<bool> CleanupAsync(
        string scratchRoot,
        string installerTempDirectory,
        bool removeInstallerTempDirectory)
    {
        var allServicesStopped = true;
        foreach (var serviceName in new[] { ServiceLifecycle.ChaptarrServiceName, ServiceLifecycle.BookshelfServiceName })
        {
            if (!ServiceLifecycle.IsInstalled(serviceName))
            {
                continue;
            }

            var imagePath = ServiceLifecycle.GetImagePath(serviceName);
            if (string.IsNullOrWhiteSpace(imagePath) ||
                !imagePath.Contains(scratchRoot, StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine($"Refusing to clean up {serviceName}; its command does not point inside the smoke-test directory.");
                allServicesStopped = false;
                continue;
            }

            try
            {
                await StopServiceAsync(serviceName).ConfigureAwait(false);
                var deleted = await ServiceLifecycle.DeleteAsync(
                    serviceName,
                    new Progress<LogLine>(line => Console.WriteLine(line.Text))).ConfigureAwait(false);
                allServicesStopped &= deleted;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Could not safely remove {serviceName}: {exception.Message}");
                allServicesStopped = false;
            }
        }

        if (!allServicesStopped)
        {
            return false;
        }

        var record = RegistryService.Read();
        if (record is not null &&
            !string.IsNullOrWhiteSpace(record.DataDirectory) &&
            Path.GetFullPath(record.DataDirectory).StartsWith(Path.GetFullPath(scratchRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            baseKey.DeleteSubKeyTree(BookshelfRegistryKey, throwOnMissingSubKey: false);
        }

        try
        {
            if (removeInstallerTempDirectory &&
                Directory.Exists(installerTempDirectory) &&
                !Directory.EnumerateFileSystemEntries(installerTempDirectory).Any())
            {
                Directory.Delete(installerTempDirectory);
            }

            if (Directory.Exists(scratchRoot))
            {
                await DeleteDirectoryWithRetryAsync(scratchRoot).ConfigureAwait(false);
            }

            return true;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not remove smoke-test directory {scratchRoot}: {exception.Message}");
            return false;
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        const int attempts = 12;
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt + 1 < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (attempt + 1 < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
            }
        }

        Directory.Delete(path, recursive: true);
    }

    private sealed class SmokeUiReporter : IUiReporter
    {
        public void SetStep(string step, string detail = "") =>
            Console.WriteLine($"[installer] {step}{(string.IsNullOrWhiteSpace(detail) ? "" : " " + detail)}");

        public void SetProgressPercent(double percent) { }
        public void SetProgressIndeterminate() { }
        public ProviderIdentityMode? ChooseProviderIdentityMode() => ProviderIdentityMode.NativeHardcover;
        public bool ConfirmMigrationApply(ProviderIdentityMode mode, string backupPath) => true;
        public void Log(LogLine line) => Console.WriteLine($"[{line.Stream}] {line.Text}");
        public void Log(string line) => Console.WriteLine(line);
        public void LogError(string line) => Console.Error.WriteLine(line);
    }

    private enum DatabaseLineageKind
    {
        Unsupported,
        BookshelfNG,
        Chaptarr
    }
}
