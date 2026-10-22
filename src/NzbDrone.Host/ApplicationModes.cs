namespace NzbDrone.Host
{
    public enum ApplicationModes
    {
        Interactive,
        Help,
        InstallService,
        UninstallService,
        Service,
        RegisterUrl,
        MigrateBookshelf,
        RestoreBookshelf,
        CheckBookshelfPostgresBackup,
        BackupBookshelfPostgres,
        RestoreBookshelfPostgres
    }
}
