namespace ChaptarrNG.Installer.Pipeline;

public enum ProviderIdentityMode
{
    Goodreads,
    NativeHardcover
}

public interface IUiReporter
{
    void SetStep(string step, string detail = "");
    void SetProgressPercent(double percent);
    void SetProgressIndeterminate();
    ProviderIdentityMode? ChooseProviderIdentityMode();
    bool ConfirmMigrationApply(ProviderIdentityMode mode, string backupPath);
    void Log(LogLine line);
    void Log(string line);
    void LogError(string line);
}
