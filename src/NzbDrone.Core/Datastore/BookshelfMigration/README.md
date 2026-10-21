# Embedded BookshelfNG SQLite migrations

The migration classes in this directory are copied from
BookshelfNG `main-v0.4.21.71` at commit
`3fad9427d167ed69d30ca6ed55f40262c2846487` under GPL-3.0. They run only on a
disposable copy of a BookshelfNG SQLite database, before the ChaptarrNG data
importer runs. The migration controller filters to this namespace, so these
version numbers cannot run against a ChaptarrNG database.

The converter keeps the consistent source snapshot untouched for rollback,
copies it to a temporary migration file, applies the released BookshelfNG
migration chain there, and requires the resulting schema to pass the importer’s
migration-158 checks. Preview removes both temporary files without changing
the source. Apply retains the untouched original schema as the rollback backup.

The copied migrations include PostgreSQL timestamp-type changes. Those type
changes are skipped for the SQLite snapshot because SQLite stores these values
with dynamic typing and the released BookshelfNG SQLite schema already uses
`DATETIME` affinity. New timestamp columns use that same SQLite-compatible
type. The isolated migration runner also includes BookshelfNG’s SQLite table
rebuild processor for its older alter, rename, and drop-column migrations.

This embedded migration chain applies to SQLite source databases. It upgrades
recognized schemas 1–158 on a disposable copy before the importer validates
the final table set and version. Unknown/newer schemas fail closed. The
automatic compatibility path does not rewrite or normalize the original
database before it is retained.

A separate PostgreSQL converter candidate is in development. It does not run
this SQLite migration chain and accepts only a current migration-158
BookshelfNG PostgreSQL schema. It stages native Chaptarr tables in a temporary
schema, retains the source tables in a backup schema after promotion, and has
preview and rollback routes. The candidate is not supported by a published
release; its hosted populated-data test, rollback evidence, and operator-data
acceptance are still open. See
[`docs/BOOKSHELFNG_POSTGRESQL_MIGRATION.md`](../../../../docs/BOOKSHELFNG_POSTGRESQL_MIGRATION.md).

When updating this embedded migration chain, keep the original source tag and
commit recorded above, review SQLite-specific behavior, and extend the
older-schema conversion, non-mutating preview, rollback, and current-schema
fixtures in `src/Chaptarr.Core.Test/Datastore/BookshelfDatabaseImporterFixture.cs`.
