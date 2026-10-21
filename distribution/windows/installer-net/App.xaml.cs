using System;
using System.Windows;
using ChaptarrNG.Installer.Pipeline;

namespace ChaptarrNG.Installer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (Array.Exists(e.Args, arg => string.Equals(arg, "/verify-payload", StringComparison.OrdinalIgnoreCase)))
        {
            var valid = PayloadExtractor.HasEmbeddedPayload();
            if (!valid)
            {
                System.Diagnostics.Trace.WriteLine("The installer is missing its embedded ChaptarrNG payload or rollback script.");
            }

            Shutdown(valid ? 0 : 1);
            return;
        }

        var restoreMode = false;
        foreach (var arg in e.Args)
        {
            if (string.Equals(arg, "/restore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--restore", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-restore", StringComparison.OrdinalIgnoreCase))
            {
                restoreMode = true;
            }
        }

        var window = new MainWindow(restoreMode);
        window.Show();
    }
}
