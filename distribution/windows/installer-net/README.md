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
3. Stop BookshelfNG and copy its `bin`, config, SQLite database, WAL and journal,
   and existing user backups into a timestamped sibling backup directory.
4. Run SQLite `PRAGMA integrity_check` against the copied database. The backup
   is retained permanently; the installer does not rotate older backups.
5. Stage the ChaptarrNG payload outside `bin` and run the non-mutating migration
   preview with the chosen provider mode. The installer hashes the database,
   WAL, and rollback journal before and after preview. SQLite rebuilds its
   transient `-shm` index when the database is reopened, so it is not backed up.
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

Windows CI also runs a focused fingerprint regression smoke: changes to the
database, WAL, or rollback journal must invalidate the preview fingerprint,
while changes to SQLite's rebuildable `-shm` index must not.

Windows CI also runs the real installer upgrade and restore actions against a
disposable service installation. The smoke downloads BookshelfNG **0.4.21.71**
from its pinned public release asset and verifies the published SHA-256 before
use. It seeds the shared populated migration-158 fixture, disables provider
sync, runs both service handoff and health checks, then invokes the installer’s
embedded rollback script and verifies BookshelfNG is running again with its
original database lineage and LocalService account. All services, registry
state, and temporary data are removed after the run; cleanup preserves the
scratch directory if a test service cannot be safely removed.

To run the round-trip locally from an elevated Windows PowerShell session after
building the ChaptarrNG payload, use:

```powershell
dotnet run --configuration Release --project ./distribution/windows/installer-net-smoke/ChaptarrNG.Installer.Smoke.csproj -- --windows-service-round-trip
```

The packaging script accepts `-KeepPayload` for this test build so the smoke
project can embed the same installer resources. CI removes that temporary zip
after the smoke. The script’s default behavior still removes it immediately
after publishing the candidate installer.

## Remaining acceptance

The code and hosted build do not replace migration rehearsal on representative
Windows BookshelfNG data. Before enabling a release or update-channel handoff,
validate:

- A populated Windows SQLite install with provider IDs and paths representative
  of real deployments.
- Acceptance against representative operator data. The hosted round-trip uses
  a synthetic fixture and does not establish real-install parity.
- Preview immutability, apply accounting, service handoff, health check, and
  automatic rollback after injected failures. The hosted service round-trip
  exercises the explicit restore action; it does not inject a failure into the
  automatic rollback branch.
- Manual restore after Chaptarr has written to the migrated database.
- Installer behavior on each supported Windows BookshelfNG version and any
  supported legacy data-directory layout.

Keep BookshelfNG running and do not apply production migrations until the
maintainers explicitly close these acceptance gates.
