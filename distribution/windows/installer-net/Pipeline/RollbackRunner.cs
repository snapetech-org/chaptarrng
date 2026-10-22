using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ChaptarrNG.Installer.Pipeline;

public static class RollbackRunner
{
    private const string ResourceName = "rollback.ps1";

    public static async Task<int> RunAsync(
        string backupPath,
        string dataDirectory,
        IProgress<LogLine> log,
        CancellationToken ct = default,
        int? bookshelfServiceStartType = null,
        bool? bookshelfServiceWasRunning = null,
        string? chaptarrConsolePath = null,
        bool restorePostgresDatabase = false)
    {
        var scriptPath = ExtractToTemp();
        try
        {
            log.Report(new LogLine(LogStream.Stdout, $"[rollback] Restoring BookshelfNG from {backupPath}..."));
            var arguments = new System.Collections.Generic.List<string>
            {
                "-BackupPath", backupPath,
                "-DataDirectory", dataDirectory
            };
            if (bookshelfServiceStartType.HasValue)
            {
                arguments.Add("-BookshelfServiceStartType");
                arguments.Add(bookshelfServiceStartType.Value.ToString());
            }
            if (bookshelfServiceWasRunning.HasValue)
            {
                arguments.Add("-BookshelfServiceWasRunning");
                arguments.Add(bookshelfServiceWasRunning.Value ? "1" : "0");
            }
            if (!string.IsNullOrWhiteSpace(chaptarrConsolePath))
            {
                arguments.Add("-ChaptarrConsolePath");
                arguments.Add(chaptarrConsolePath);
            }
            if (restorePostgresDatabase)
            {
                arguments.Add("-RestorePostgresDatabase");
            }

            var exit = await PowerShellRunner.RunScriptAsync(
                scriptPath,
                arguments,
                log,
                ct).ConfigureAwait(false);
            log.Report(new LogLine(LogStream.Stdout, $"[rollback] rollback.ps1 exited {exit}."));
            return exit;
        }
        finally
        {
            try { File.Delete(scriptPath); }
            catch { /* Keep cleanup best-effort; the restore result is more important. */ }
        }
    }

    private static string ExtractToTemp()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found in the installer.");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "ChaptarrNG.Installer");
        Directory.CreateDirectory(tempDirectory);
        var scriptPath = Path.Combine(tempDirectory, $"rollback-{Guid.NewGuid():N}.ps1");

        using var destination = File.Create(scriptPath);
        stream.CopyTo(destination);
        return scriptPath;
    }
}
