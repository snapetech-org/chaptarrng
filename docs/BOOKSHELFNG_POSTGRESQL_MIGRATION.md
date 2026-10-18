# BookshelfNG PostgreSQL migration candidate

> **Not supported in a published release.** ChaptarrNG `v0.9.977` cannot
> convert a PostgreSQL BookshelfNG database. The converter in this work branch
> is a candidate for one BookshelfNG schema at migration 158. Do not use it for
> operator data until hosted PostgreSQL checks, populated-data review, and
> rollback acceptance are complete and a release explicitly supports it.

## Candidate behavior

The candidate keeps the BookshelfNG database in the same PostgreSQL database.
It copies the source tables into a temporary schema, migrates a fresh Chaptarr
schema in a second temporary schema, imports the snapshot, and checks lineage
and per-table row disposition before promotion. Promotion moves the original
BookshelfNG tables and sequences into a `bookshelfng_backup_*` schema, then
moves the Chaptarr tables into the schema selected by the existing connection
search path. Rollback moves the retained BookshelfNG tables back and preserves
the displaced Chaptarr tables in a `chaptarrng_backup_*` schema.

The schema backups remain in the same PostgreSQL database. They are not a
replacement for an independent database backup. The converter expects the
BookshelfNG application to be stopped; its advisory lock coordinates ChaptarrNG
conversions but does not stop BookshelfNG or other database clients from
writing.

The candidate accepts only the current BookshelfNG migration-158 main schema.
For an older schema, run the final BookshelfNG `0.4.21.71` release against its
own database first. The converter rejects unsupported tables and PostgreSQL
views, materialized views, foreign tables, and partitioned tables before
promotion.

The candidate importer translates a saved `HardcoverAuth` config row to
ChaptarrNG's `HardcoverApiToken` setting when the source has no explicit
ChaptarrNG token. The original BookshelfNG row remains in the source backup;
the migration report describes the mapping without including the token value.
The exact hosted run on `e67f28ed` failed the populated fixture because
migration 10 checked `public.Editions` rather than the active staging schema
and skipped the staging `Subtitle` column. Commit `8f0bbc29` adds the column
against the active PostgreSQL search path and verifies the runner's version-
table schema. The exact run on `8f0bbc29` then failed to compile because
`IMigrationRunner` has no `VersionLoader` property. Commit `50f58223` reads the
registered `IVersionTableMetaData` service instead; that run then failed at
migration 23 because an unqualified schema check examined
`public.NamingConfig`, skipped the staging `EbookRenameBooks` column, and the
following update failed. The working tree now routes unqualified PostgreSQL
schema checks to the active schema and adds a focused regression test; the
correction awaits exact-head CI. This candidate remains unsupported for
operator data until it passes and the remaining acceptance checks pass.
Numeric provider IDs follow the active
BookshelfNG mode: run preview and conversion
with `HARDCOVER=true` and without `HARDCOVER_NATIVE=false` when the source uses
native Hardcover, or the importer will interpret bare numbers as Goodreads
IDs. The preview report states the interpretation applied to authors, books,
editions, and series.

## Command shape after support is released

Use the same ChaptarrNG PostgreSQL main-database connection settings and app
data directory for preview, apply, and rollback. The exact option names are
available in the ChaptarrNG utility help:

```sh
dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config

dotnet /app/Chaptarr.dll -migrate-bookshelf \
  -confirm-bookshelf-service-stopped -data=/config

dotnet /app/Chaptarr.dll -preview-bookshelf-restore \
  -bookshelf-backup=bookshelfng_backup_YYYYMMDDHHMMSS_ID -data=/config

dotnet /app/Chaptarr.dll -restore-bookshelf \
  -confirm-bookshelf-service-stopped \
  -bookshelf-backup=bookshelfng_backup_YYYYMMDDHHMMSS_ID -data=/config
```

Apply and rollback require the explicit stopped-service confirmation. Save the
backup schema name printed by apply. A PostgreSQL backup argument is a schema
name, not a filesystem path. Review files for ambiguous media choices are
stored beside `readarr.db` in the ChaptarrNG app data directory.

## Acceptance still required

- Pass the hosted populated PostgreSQL conversion and rollback fixture.
- Verify PostgreSQL BookshelfNG migrations and ChaptarrNG runtime startup in
  the supported deployment, including sequence allocation after import.
- Rehearse a representative operator database and review every ambiguous media
  choice, preserved identity, setting, file link, and history disposition.
- Restore from the schema backup and from an independent `pg_dump` backup.
- Close the BookshelfNG feature-parity matrix and each package, image, updater,
  and install-channel handoff before enabling any release or legacy bridge.

Until those checks pass, use the supported SQLite route in the
[BookshelfNG migration guide](MIGRATE_FROM_BOOKSHELFNG.md), or keep the existing
BookshelfNG PostgreSQL installation in service.
