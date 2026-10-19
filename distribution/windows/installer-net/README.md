# ChaptarrNG Windows BookshelfNG migration installer

This is a **candidate-only, migration-only** Windows installer. It is not
published or linked from a release channel while BookshelfNG retirement remains
release-frozen. It does not install a fresh ChaptarrNG instance.

The installer upgrades an existing BookshelfNG Windows service from `Readarr`
to `Chaptarr`, keeping its selected data directory. The initial supported path
is a single SQLite database in an existing BookshelfNG installation using its
standard `NT AUTHORITY\LocalService` service account. Custom service accounts
fail closed so the installer cannot silently change access to media folders or
network shares. PostgreSQL,
multiple appdata databases, unsupported schemas, and installations without the
legacy Windows service fail closed and use the documented manual migration
procedure instead.

## Migration sequence

The installer requires administrator privileges and performs this sequence:

1. Detect the BookshelfNG `Readarr` service and its configured `-data` directory.
2. Ask whether bare numeric provider IDs represent native Hardcover or Goodreads.
3. Stop BookshelfNG and copy its `bin`, config, SQLite database and sidecar files,
   and existing user backups into a timestamped sibling backup directory.
4. Run SQLite `PRAGMA integrity_check` against the copied database. The backup
   is retained permanently; the installer does not rotate older backups.
5. Stage the ChaptarrNG payload outside `bin` and run the non-mutating migration
   preview with the chosen provider mode. The installer hashes the database and
   all SQLite sidecars before and after preview.
6. Show the preview output in the installer log and require a separate apply
   confirmation.
7. Apply the migration, replace `bin`, remove the `Readarr` service, and register
   `Chaptarr` with the same explicit data directory.
8. Verify the service reaches `RUNNING`, confirm its service command still
   contains the data directory, and check the configured local `/ping` endpoint.

Any failure after a complete backup invokes the embedded rollback helper. It
stops and removes either service registration, restores the saved BookshelfNG
binaries, config, database, and user backups, then reinstalls and starts
`Readarr`. The backup is preserved after both success and failure. The UI also
offers **Restore BookshelfNG** after a successful migration. Manual restore
returns to the backup point, so Chaptarr changes made after migration are not
kept.

The preview and apply commands run with `HARDCOVER` set only in those child
processes. Native Hardcover mode sets `HARDCOVER=true`; Goodreads mode sets
`HARDCOVER=false`. No provider tokens are written to the registry or installer
logs.

## Build and candidate artifact

The Windows CI build packages the already-published ChaptarrNG application
files into the installer as an embedded zip. The output is an internal CI
artifact for validation; this step does not create or publish a release.

From a Windows PowerShell session after building the application into
`publish/Chaptarr`:

```powershell
./packaging/scripts/build-windows-bookshelf-migration-installer.ps1
```

The candidate executable is written to
`publish/ChaptarrNGInstaller/ChaptarrNG.Installer.exe`. The build script refuses
to overwrite an existing payload or output directory and removes the temporary
embedded zip after publishing.

## Remaining acceptance

The code and hosted build do not replace migration rehearsal on representative
Windows BookshelfNG data. Before enabling a release or update-channel handoff,
validate:

- A populated Windows SQLite install with provider IDs and paths representative
  of real deployments.
- Preview immutability, apply accounting, service handoff, health check, and
  automatic rollback after injected failures.
- Manual restore after Chaptarr has written to the migrated database.
- Installer behavior on each supported Windows BookshelfNG version and any
  supported legacy data-directory layout.

Keep BookshelfNG running and do not apply production migrations until the
maintainers explicitly close these acceptance gates.
