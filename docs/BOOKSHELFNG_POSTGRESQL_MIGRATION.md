# BookshelfNG PostgreSQL migration candidate

> **Not supported in a published release.** ChaptarrNG `v0.9.977` cannot
> convert a PostgreSQL BookshelfNG database. The converter in this work branch
> is a candidate for one BookshelfNG schema at migration 158. Do not use it for
> operator data until representative populated-data review and rollback
> acceptance are complete and a release explicitly supports it. Exact-head run
> [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
> passed on PR #131 head 2c0401fc: Linux, macOS, and Windows build/tests,
> PostgreSQL migration fixtures, release-note validation, CodeQL, dependency
> and secret checks, NuGet submission, and runner-receipt reconciliation. The
> PostgreSQL fixture passed all four candidate cases. Disposable-fork
> validation was skipped by its gate. This validates one synthetic
> migration-158 schema only; PostgreSQL operator-data acceptance and
> independent backup/restore validation remain open.

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
Earlier hosted runs found and corrected staging-schema lookup and migration-runner API issues. Run [39125697386](https://github.com/snapetech-org/chaptarrng/actions/runs/39125697386) on `fb99a694` compiled the candidate but failed because the smoke runner could not produce a valid NUnit summary. Its receipt contained no test totals or failed cases, so it did not establish a converter assertion failure. Commit `8ab6100a` adds bounded diagnostics for the NUnit result-file state. Run [39126322692](https://github.com/snapetech-org/chaptarrng/actions/runs/39126322692) failed after about 90 seconds with a missing NUnit summary, no totals, and no safe diagnostic; the cause is unconfirmed.

Run [39132760214](https://github.com/snapetech-org/chaptarrng/actions/runs/39132760214) on `9c051d3d` reported NUnit source line 162 in `should_preview_convert_and_restore_a_current_bookshelf_postgres_schema`. The fixture seeded `BookImportMinimumMatchPercent` with its original casing but checked lowercase, a fixture expectation error. ChaptarrNG commit [b5bd9527](https://github.com/snapetech-org/chaptarrng/commit/b5bd9527) corrects all three retained-config checks. Exact-head run [39135608892](https://github.com/snapetech-org/chaptarrng/actions/runs/39135608892) on commit [`43bc6363`](https://github.com/snapetech-org/chaptarrng/commit/43bc6363) passed Linux, macOS, and Windows platform validation and the release-note contract; all 4 synthetic PostgreSQL migration cases also passed, including normal-startup conversion with backup retention. This validates the migration-158 fixture only. The candidate remains unsupported for operator data and unpublished pending representative populated-data conversion and rollback, parity, and channel acceptance.
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

- Hosted synthetic PostgreSQL conversion and rollback fixture: passed 4/4 cases on exact-head run [39175250100](https://github.com/snapetech-org/chaptarrng/actions/runs/39175250100), including normal-startup conversion and backup retention. This is one synthetic migration-158 schema. Representative operator data, independent backup/restore, and rollback acceptance on a supported deployment remain open.
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
