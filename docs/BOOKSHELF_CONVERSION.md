# BookshelfNG database conversion

The current ChaptarrNG source tree can stage a conversion of the exact
BookshelfNG v158 SQLite schema. This code is not in the published `0.9.969`
release, does not migrate PostgreSQL databases, and does not provide an
upgrade bridge for Docker, Unraid, or other release channels. The importer has
synthetic database fixtures, but it has not been validated against a populated
production database. Keep an independent backup and test a disposable copy
before using this source-only path with real data.

## How database selection works

ChaptarrNG uses a non-empty `chaptarr.db` when it exists. If that file is
missing or empty, it can select one non-empty legacy `readarr.db` or
`audioarr.db`. If both legacy files are present, startup stops instead of
choosing one arbitrarily. If a non-empty legacy database sits beside a
non-empty `chaptarr.db`, startup also stops so the legacy database is not
silently ignored.

Only a recognized BookshelfNG SQLite database enters the converter. Unknown,
partial, or ordinary Readarr schemas fail before numbered Chaptarr migrations
run. A version-targeted migration also cannot trigger conversion; conversion
requires the full startup migration.

## Conversion and safeguards

The converter creates a consistent SQLite snapshot, migrates a separate
staging database to the current Chaptarr schema, and imports the source rows
inside a transaction. It accounts for every source table and row. Unsupported
schema versions, unexpected tables, unknown provider identifiers, ambiguous
monitored editions, or relationships that cannot be mapped stop the conversion
before the source database is replaced. Transient queues and health state are
explicitly reset because they are recreated by Chaptarr.

Before promotion, the converter checks SQLite integrity, foreign keys, and the
staged database lineage. It then retains a timestamped
`*.bookshelfng-backup-*.db` file beside the selected database and replaces the
source path with the staged Chaptarr database. The application logs the backup
path. If import or validation fails before promotion, the source database stays
in place.

The converter migrates database contents only. It does not copy media files or
prove that existing package, configuration, updater, client, or rollback
workflows continue to work. Book and edition identities, histories, root
folders, profiles, file records, provider IDs, and other persisted settings
must be validated against the exact BookshelfNG instance and Chaptarr release
before relying on the result.

## Recovery and validation

Keep the original database backup outside the application data directory as
well as the converter's retained backup. If you need to restore one, stop
Chaptarr first, preserve the failed converted database for diagnosis, and
restore the saved database to its original path before restarting.

The converter, importer, schema-lineage, migration-reconciliation, and legacy
database-selection fixtures exercise synthetic SQLite databases and fail-closed
cases. Those tests do not establish compatibility with real BookshelfNG data or
with a released deployment channel. PostgreSQL conversion and channel-specific
upgrade and rollback bridges remain out of scope.
