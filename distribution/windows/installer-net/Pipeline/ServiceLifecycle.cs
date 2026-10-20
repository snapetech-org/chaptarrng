using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ChaptarrNG.Installer.Pipeline;

public static class ServiceLifecycle
{
    public const string BookshelfServiceName = "Readarr";
    public const string ChaptarrServiceName = "Chaptarr";
    private const string DefaultBookshelfDataDirectory = @"C:\ProgramData\Readarr";
    private static readonly TimeSpan ServiceProcessExitTimeout = TimeSpan.FromSeconds(15);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(
        IntPtr serviceHandle,
        int infoLevel,
        out ServiceStatusProcess status,
        uint bufferSize,
        out uint bytesNeeded);

    public static string? GetImagePath(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
            writable: false);
        return key?.GetValue("ImagePath") as string;
    }

    private static bool IsServiceRegistrationPresent(string serviceName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{serviceName}",
            writable: false);
        return key is not null;
    }

    public static string GetBookshelfServiceAccount()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            $@"SYSTEM\CurrentControlSet\Services\{BookshelfServiceName}",
            writable: false);
        return key?.GetValue("ObjectName") as string ?? "LocalSystem";
    }

    public static string GetBookshelfDataDirectory()
    {
        var imagePath = GetImagePath(BookshelfServiceName);
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return DefaultBookshelfDataDirectory;
        }
        imagePath = Environment.ExpandEnvironmentVariables(imagePath);

        var dataMatch = Regex.Match(
            imagePath,
            @"(?i)(?:/|-{1,2})data\s*=\s*(?:""(?<quoted>[^""]+)""|(?<bare>[^\s]+))");
        if (dataMatch.Success)
        {
            var value = dataMatch.Groups["quoted"].Success
                ? dataMatch.Groups["quoted"].Value
                : dataMatch.Groups["bare"].Value;
            value = Environment.ExpandEnvironmentVariables(value);
            if (!Path.IsPathRooted(value))
            {
                throw new InvalidOperationException(
                    $"The BookshelfNG service uses a relative data directory ('{value}'). Set an absolute path before migrating.");
            }

            return Path.GetFullPath(value);
        }

        var executable = GetExecutablePath(imagePath);
        if (executable is not null)
        {
            var binDirectory = Path.GetDirectoryName(executable);
            if (binDirectory is not null &&
                string.Equals(Path.GetFileName(binDirectory), "bin", StringComparison.OrdinalIgnoreCase))
            {
                var dataDirectory = Directory.GetParent(binDirectory)?.FullName;
                if (dataDirectory is not null)
                {
                    return dataDirectory;
                }
            }
        }

        return DefaultBookshelfDataDirectory;
    }

    public static bool IsInstalled(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            _ = sc.Status;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static ServiceControllerStatus? TryGetStatus(string serviceName)
    {
        try
        {
            using var sc = new ServiceController(serviceName);
            return sc.Status;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static async Task<bool> StopAsync(
        string serviceName,
        IProgress<LogLine> log,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (!IsInstalled(serviceName))
        {
            log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} is not installed; nothing to stop."));
            return true;
        }

        using var sc = new ServiceController(serviceName);
        try
        {
            sc.Refresh();
            var processId = GetServiceProcessId(sc);
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} already stopped."));
            }
            else
            {
                log.Report(new LogLine(LogStream.Stdout, $"[service] Stopping {serviceName}..."));
                if (sc.Status != ServiceControllerStatus.StopPending)
                {
                    sc.Stop();
                }

                var deadline = DateTime.UtcNow + timeout;
                while (DateTime.UtcNow < deadline)
                {
                    ct.ThrowIfCancellationRequested();
                    sc.Refresh();
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} reported STOPPED."));
                        break;
                    }

                    await Task.Delay(500, ct).ConfigureAwait(false);
                }

                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    log.Report(new LogLine(LogStream.Stderr, $"[service] Timed out waiting for {serviceName} to stop."));
                    return false;
                }
            }

            if (processId.HasValue && !await WaitForProcessExitAsync(serviceName, processId.Value, log, ct).ConfigureAwait(false))
            {
                return false;
            }

            log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} process has exited."));
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            log.Report(new LogLine(LogStream.Stderr, $"[service] Could not stop {serviceName}: {ex.Message}"));
            return false;
        }
    }

    public static async Task<bool> StartAsync(
        string serviceName,
        IProgress<LogLine> log,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        if (!IsInstalled(serviceName))
        {
            log.Report(new LogLine(LogStream.Stderr, $"[service] {serviceName} is not installed; it cannot be started."));
            return false;
        }

        using var sc = new ServiceController(serviceName);
        try
        {
            sc.Refresh();
            if (sc.Status == ServiceControllerStatus.Stopped)
            {
                log.Report(new LogLine(LogStream.Stdout, $"[service] Starting {serviceName}..."));
                sc.Start();
            }

            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                sc.Refresh();
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} is RUNNING."));
                    return true;
                }

                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            log.Report(new LogLine(LogStream.Stderr, $"[service] Timed out waiting for {serviceName} to start."));
            return false;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            log.Report(new LogLine(LogStream.Stderr, $"[service] Could not start {serviceName}: {ex.Message}"));
            return false;
        }
    }

    public static async Task<bool> DeleteAsync(
        string serviceName,
        IProgress<LogLine> log,
        CancellationToken ct = default)
    {
        if (!IsInstalled(serviceName))
        {
            log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} is not installed; skipping delete."));
            return true;
        }

        log.Report(new LogLine(LogStream.Stdout, $"[service] Deleting service {serviceName}..."));
        var exit = await ProcessStreamer.RunAsync(
            "sc.exe",
            new[] { "delete", serviceName },
            null,
            log,
            ct).ConfigureAwait(false);
        if (exit != 0)
        {
            log.Report(new LogLine(LogStream.Stderr, $"[service] sc.exe delete {serviceName} exited {exit}."));
            return false;
        }

        // Windows may keep a stopped service marked for deletion until every
        // outstanding SCM handle closes. Give service-host and management
        // handles enough time to drain before reporting that removal failed.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            // Do not poll through ServiceController after DeleteService. Opening
            // the service while it is marked for deletion can keep its SCM
            // registration alive and delay the very removal we are waiting for.
            if (!IsServiceRegistrationPresent(serviceName))
            {
                return true;
            }

            await Task.Delay(250, ct).ConfigureAwait(false);
        }

        return !IsServiceRegistrationPresent(serviceName);
    }

    public static async Task<bool> PollForRunningAsync(
        string serviceName,
        IProgress<LogLine> log,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        log.Report(new LogLine(LogStream.Stdout, $"[service] Waiting for {serviceName} to reach RUNNING ({timeout.TotalSeconds:F0}s timeout)..."));
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var status = TryGetStatus(serviceName);
            if (status == ServiceControllerStatus.Running)
            {
                log.Report(new LogLine(LogStream.Stdout, $"[service] {serviceName} is RUNNING."));
                return true;
            }

            await Task.Delay(2000, ct).ConfigureAwait(false);
        }

        log.Report(new LogLine(LogStream.Stderr, $"[service] {serviceName} did not reach RUNNING within {timeout.TotalSeconds:F0}s."));
        return false;
    }

    private static string? GetExecutablePath(string imagePath)
    {
        var value = imagePath.TrimStart();
        if (value.StartsWith('"'))
        {
            var endQuote = value.IndexOf('"', 1);
            return endQuote > 1 ? value[1..endQuote] : null;
        }

        var exeEnd = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeEnd >= 0 ? value[..(exeEnd + 4)].Trim() : null;
    }

    private static int? GetServiceProcessId(ServiceController service)
    {
        if (!QueryServiceStatusEx(
                service.ServiceHandle.DangerousGetHandle(),
                0,
                out var status,
                (uint)Marshal.SizeOf<ServiceStatusProcess>(),
                out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not query the process ID for service {service.ServiceName}.");
        }

        return status.ProcessId == 0 ? null : checked((int)status.ProcessId);
    }

    private static async Task<bool> WaitForProcessExitAsync(
        string serviceName,
        int processId,
        IProgress<LogLine> log,
        CancellationToken ct)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return true;
        }

        using (process)
        {
            if (process.HasExited)
            {
                return true;
            }

            log.Report(new LogLine(
                LogStream.Stdout,
                $"[service] Waiting for {serviceName} process {processId} to exit after the service reported STOPPED..."));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ServiceProcessExitTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    return true;
                }

                log.Report(new LogLine(
                    LogStream.Stderr,
                    $"[service] {serviceName} process {processId} did not exit within {ServiceProcessExitTimeout.TotalSeconds:0} seconds."));
                return false;
            }
        }
    }
}
