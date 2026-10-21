using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ChaptarrNG.Installer.Pipeline;

namespace ChaptarrNG.Installer;

public partial class MainWindow : IUiReporter
{
    private bool _autoScroll = true;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private readonly bool _restoreMode;

    public MainWindow() : this(restoreMode: false) { }

    public MainWindow(bool restoreMode)
    {
        _restoreMode = restoreMode;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_restoreMode)
        {
            Title = "ChaptarrNG BookshelfNG Migration — Restore";
            InstallButton.Visibility = Visibility.Collapsed;
            RestoreButton.Visibility = Visibility.Collapsed;
            var confirm = MessageBox.Show(
                this,
                "This replaces the migrated database and binaries with the retained BookshelfNG backup, removes the Chaptarr service, and starts Readarr. Chaptarr changes made after migration will be lost. Continue?",
                "Confirm BookshelfNG restore",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                Close();
                return;
            }

            AppendLog("[installer] /restore mode — restoring the retained BookshelfNG migration backup.");
            await RunRestoreAsync().ConfigureAwait(true);
            return;
        }

        if (RegistryService.HasRecoverableUpgrade())
        {
            var record = RegistryService.Read();
            RestoreButton.Visibility = Visibility.Visible;
            AppendLog("[installer] A verified BookshelfNG migration backup was found — Restore BookshelfNG is available.");
            if (!string.IsNullOrWhiteSpace(record?.PreviousVersion))
            {
                AppendLog($"[installer]   PreviousVersion: {record.PreviousVersion}");
            }
            if (!string.IsNullOrWhiteSpace(record?.BackupPath))
            {
                AppendLog($"[installer]   BackupPath: {record.BackupPath}");
            }
        }
    }

    private async Task RunRestoreAsync()
    {
        _isRunning = true;
        _cts = new CancellationTokenSource();
        SetButtonsForRunning();
        CancelButton.IsEnabled = false;

        try
        {
            var outcome = await Task.Run(() => InstallPipeline.RunRestoreAsync(this, _cts.Token))
                .ConfigureAwait(true);

            if (!outcome.Success)
            {
                AppendLogColored($"[installer] Restore failed: {outcome.ErrorMessage}", Color.FromRgb(0xE8, 0x4A, 0x4A));
                MessageBox.Show(this,
                    outcome.ErrorMessage ?? "Restore failed.",
                    "ChaptarrNG Installer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
            SetButtonsForDone();
        }
    }

    // --- IUiReporter (always marshal to UI thread) ---

    public void SetStep(string step, string detail = "")
    {
        Dispatcher.Invoke(() =>
        {
            StepLabel.Text = step;
            DetailLabel.Text = detail;
        });
    }

    public void SetProgressPercent(double percent)
    {
        Dispatcher.Invoke(() =>
        {
            Progress.IsIndeterminate = false;
            Progress.Value = Math.Clamp(percent, 0, 100);
        });
    }

    public void SetProgressIndeterminate()
    {
        Dispatcher.Invoke(() => Progress.IsIndeterminate = true);
    }

    public ProviderIdentityMode? ChooseProviderIdentityMode()
    {
        return Dispatcher.Invoke(() =>
        {
            var result = MessageBox.Show(
                this,
                "BookshelfNG can store bare numeric provider IDs for either native Hardcover or Goodreads records.\n\n" +
                "Choose Yes if the primary Bookshelf catalog uses native Hardcover IDs.\n" +
                "Choose No if the primary catalog uses Goodreads IDs.\n" +
                "If you are unsure, choose Cancel and inspect the BookshelfNG provider settings first.",
                "Choose BookshelfNG provider identity",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            return result switch
            {
                MessageBoxResult.Yes => ProviderIdentityMode.NativeHardcover,
                MessageBoxResult.No => ProviderIdentityMode.Goodreads,
                _ => (ProviderIdentityMode?)null
            };
        });
    }

    public bool ConfirmMigrationApply(ProviderIdentityMode mode, string backupPath)
    {
        return Dispatcher.Invoke(() =>
        {
            var providerName = mode == ProviderIdentityMode.NativeHardcover
                ? "native Hardcover IDs"
                : "Goodreads IDs";
            var result = MessageBox.Show(
                this,
                "The read-only preview completed. Review its row counts, mappings, and warnings in the installer log before continuing.\n\n" +
                $"The apply step will modify the BookshelfNG database using {providerName}. The original binaries, config, database, and user backups are saved at:\n\n" +
                backupPath +
                "\n\nAfter apply, the installer switches the Windows service to ChaptarrNG. Continue?",
                "Confirm BookshelfNG migration",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        });
    }

    public void Log(LogLine line)
    {
        if (line.Stream == LogStream.Stderr)
        {
            LogError(line.Text);
        }
        else
        {
            Log(line.Text);
        }
    }

    public void Log(string line) => Dispatcher.Invoke(() => AppendLog(line));
    public void LogError(string line) => Dispatcher.Invoke(() => AppendLogColored(line, Color.FromRgb(0xE8, 0x4A, 0x4A)));

    // --- Internal log rendering ---

    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        LogText.Inlines.Add(new Run(line));
        LogText.Inlines.Add(new LineBreak());

        if (_autoScroll)
        {
            LogScroller.ScrollToEnd();
        }
    }

    private void AppendLogColored(string line, Color color)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        LogText.Inlines.Add(new Run(line) { Foreground = new SolidColorBrush(color) });
        LogText.Inlines.Add(new LineBreak());

        if (_autoScroll)
        {
            LogScroller.ScrollToEnd();
        }
    }

    private void LogScroller_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (Math.Abs(e.ExtentHeightChange) < 0.5)
        {
            // User-initiated scroll — update auto-scroll state based on whether they're at the bottom.
            _autoScroll = e.VerticalOffset >= LogScroller.ScrollableHeight - 2;
        }
    }

    // --- Button state machine ---

    private void SetButtonsForIdle()
    {
        InstallButton.IsEnabled = true;
        CancelButton.IsEnabled = false;
        CloseButton.IsEnabled = true;
    }

    private void SetButtonsForRunning()
    {
        InstallButton.IsEnabled = false;
        RestoreButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        CloseButton.IsEnabled = false;
    }

    private void SetButtonsForDone()
    {
        InstallButton.IsEnabled = false;
        if (!_restoreMode && RegistryService.HasRecoverableUpgrade())
        {
            RestoreButton.Visibility = Visibility.Visible;
            RestoreButton.IsEnabled = true;
        }
        else
        {
            RestoreButton.IsEnabled = false;
        }
        CancelButton.IsEnabled = false;
        CloseButton.IsEnabled = true;
    }

    // --- Button handlers ---

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;
        _isRunning = true;
        _cts = new CancellationTokenSource();
        SetButtonsForRunning();

        try
        {
            var outcome = await Task.Run(() => InstallPipeline.RunUpgradeAsync(this, _cts.Token))
                .ConfigureAwait(true);

            if (!outcome.Success)
            {
                AppendLogColored($"[installer] FAILED: {outcome.ErrorMessage}", Color.FromRgb(0xE8, 0x4A, 0x4A));
                if (!string.IsNullOrWhiteSpace(outcome.BackupPath))
                {
                    AppendLog($"[installer] Backup preserved at: {outcome.BackupPath}");
                }
                MessageBox.Show(this,
                    outcome.ErrorMessage ?? "Installation failed.",
                    "ChaptarrNG Installer",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            _isRunning = false;
            _cts?.Dispose();
            _cts = null;
            SetButtonsForDone();
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning) return;

        var confirm = MessageBox.Show(this,
            "Restore BookshelfNG from the verified migration backup? This returns the database to its pre-migration state and removes the Chaptarr service. Chaptarr changes made after migration will be lost.",
            "Confirm BookshelfNG restore",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        await RunRestoreAsync().ConfigureAwait(true);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts is null) return;
        AppendLogColored("[installer] Cancel requested — stopping the current step. Rollback will run if a complete backup exists.", Colors.Goldenrod);
        _cts.Cancel();
        CancelButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
