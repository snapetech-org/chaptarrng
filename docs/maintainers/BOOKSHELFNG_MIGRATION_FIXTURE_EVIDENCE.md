# BookshelfNG migration fixture evidence

**Reviewed:** 2026-10-18. **Scope:** synthetic data created by the published
BookshelfNG `0.4.21.71` Linux binary and tested with an unpublished ChaptarrNG
candidate. This is maintainer evidence, not approval to cut over operator data.

## Candidate and fixture

- ChaptarrNG source: draft PR [#131](https://github.com/snapetech-org/chaptarrng/pull/131),
  commit `1a0a1cf78b404fe86d7b266facd8e843e435d9b4`.
- Exact-head Build run:
  [39169786430](https://github.com/snapetech-org/chaptarrng/actions/runs/39169786430).
  Linux, macOS, and Windows builds/tests, PostgreSQL target migrations and
  BookshelfNG converter fixtures, release-note validation, and validation
  receipt reconciliation passed. CodeQL, dependency review, and
  dependency/secret scan checks passed on the same head. PR #131 remains
  draft.
- The Linux artifact reports ChaptarrNG `0.9.978.0`. Its `Chaptarr` apphost
  SHA-256 is
  `dfdaae95b9cbd6a4cfe44cfe042f7e6ef87ca60402be00fa8376dc1d9fea1194`; the
  `Chaptarr.Core.dll` SHA-256 is
  `19852117505d35c9f5819ac8a5a2583d3ff01c2e79e99de1cc3fe1e2fd211e52`.
- Source: the published BookshelfNG `0.4.21.71` Linux x64 release archive,
  SHA-256
  `4197d53b757ebe99642fb8f4213f963dd54641401d1f188ab6daadcb3b024d5d`; its
  extracted `Readarr` executable SHA-256 is
  `0d7c82722b1948cc4d33de139698050944b1f4f1647006836364cd9b736e5f6b`.
- The fixture helper started that released binary on loopback, upgraded a
  disposable database to schema 158, and populated linked records. No
  production backup, real credentials, external service, or real media was
  used. Fixture credentials and paths are synthetic.

The fixture contains one author, one book/work, two editions (ebook and
audiobook), two book files, one extra file, one metadata sidecar, one series
and link, one user, one notification, one history row, one download-history
row, one blocklist row, two roots, one tag, and six settings. It uses bare
numeric Hardcover IDs as stored by BookshelfNG native Hardcover mode. The
settings cover a fake Hardcover token, match threshold `75`, supplemental
catalogs, per-field source preferences, and a fake Google Books key. The
source `NamingConfig` table is empty, matching the default-settings case.

## Runtime results

The following checks used separate disposable copies of the fixture and the
Linux artifact from run `39169786430`:

- **Preview:** passed. The SHA-256 of `readarr.db` was unchanged
  (`2e3bd5e423f543e19244b5105d53f2651fc52f2a05523a24f42e040c7961d55b`).
- **Explicit apply:** passed with the required
  `-confirm-bookshelf-service-stopped` option. The target reached ChaptarrNG
  schema 121, passed SQLite `integrity_check`, and had no foreign-key errors.
  One BookshelfNG backup remained at schema 158 with its original author/book
  rows and empty naming-settings table.
- **Naming defaults:** the imported target retained one seeded ChaptarrNG
  naming row (`MultiEpisodeStyle = 0`) when BookshelfNG had no saved row. The
  source importer fixture also verifies that saved BookshelfNG naming settings
  replace the target defaults without leaving a duplicate row.
- **Data and settings:** the single source work became separate ebook and
  audiobook catalog records. Both editions and files remained; the extra
  file, metadata sidecar, series relationship, user, notification, history,
  download history, blocklist, roots, and tag were retained. The native
  Hardcover author ID became `hc:9001`, the book ID became `hc:9002`, and six
  provider alias rows were created. Ebook/audiobook quality and metadata
  profile overrides remained attached to their formats. The saved Hardcover
  credential mapped to `HardcoverApiToken`; the source setting remained in the
  backup. Threshold `75` mapped to `Balanced` as a documented lossy
  approximation, and the supplemental catalog preferences and fake Google
  Books key were retained. BookLore notification settings and triggers were
  retained unchanged.
- **Automatic startup:** passed in a network-isolated container. ChaptarrNG
  converted the source, retained one schema-158 BookshelfNG backup, and
  reached schema 121 with one author and two format-specific book records.
  SQLite integrity and foreign-key checks passed. Startup logs had no error,
  fatal, or author-path-repair failure lines. The API returned the migrated
  author, audiobook, ebook, provider identities, and one file for each format.
- **SeerrNG integration:** `scripts/chaptarrng-integration-smoke.mjs` passed
  against this populated candidate and SeerrNG source commit `0b267d526`. It
  verified status, capability discovery, one audiobook through SeerrNG's
  format-scoped paged-library adapter, and denial of an administrative route
  and unrelated command with the restricted key. The raw ChaptarrNG library
  exposed both the audiobook and ebook records. This did not exercise
  end-to-end SeerrNG request creation, history, pending-import retries, or
  existing operator settings.
- **Restore:** preview did not change the target or source backup. Explicit
  restore returned the database to schema 158 with the original author/book
  rows, passed integrity and foreign-key checks, and retained the schema-121
  ChaptarrNG database separately.
- **Fail-closed cases:** apply without stopped-service confirmation, two
  populated source database candidates, and future schema 159 were rejected.
  Each source database hash remained unchanged and no conversion backup was
  created.

## Docker image-switch and rollback rehearsal (2026-10-18)

A second, isolated rehearsal used the public BookshelfNG `.71` Hardcover image
(`ghcr.io/snapetech-org/bookshelfng:hardcover-v0.4.21.71`, digest
`sha256:fc9c8b5b91808d10cbe66dc2338243b2db79452778640c6963493363c389430b`).
The release fixture helper created a populated synthetic schema-158 database;
the source container reported BookshelfNG `0.4.21.71` and served its author and
book through the API.

An initial Chaptarr candidate container was started without the source image's
`HARDCOVER=true` setting. It promoted the bare numeric IDs into Goodreads
fields. Repeating the same image switch with `HARDCOVER=true` preserved author
`hc:9001`, book `hc:9002`, and editions `hc:9003` and `hc:9004` in the
Hardcover fields. The converted database reached Chaptarr schema 121, passed
SQLite integrity checks, had no foreign-key errors, retained the BookshelfNG
source backup, and served the migrated author plus both format-specific book
records through the Chaptarr API.

With Chaptarr stopped, rollback preview left both database hashes unchanged.
Explicit rollback restored the source schema-158 database byte-for-byte from
its retained backup and kept the converted Chaptarr database as a separate
backup. The released `.71` Hardcover image then reopened the restored source
database and served the author and book API records again.

This exposed a provider-mode handoff hazard in the prior candidate. The current
source change makes migration fail closed if bare numeric IDs are present and
`HARDCOVER` is unset or invalid, and the Docker release workflow now builds
separate softcover and Hardcover compatibility targets for the legacy aliases.
Those changes are source-only; their exact-head hosted build and channel checks
are still pending. This rehearsal used a synthetic fixture and does not
establish behavior on operator data or validate the Unraid template handoff.

These results show the candidate's SQLite preview, apply, automatic startup,
data mapping, refusal, rollback, and populated API paths working together on
this synthetic fixture. They do not establish correct classification of
ambiguous real records, PostgreSQL operator-data safety, behavior for every
historical schema, complete provider/client continuity, end-to-end request
parity, or installation-channel behavior. Do not use this candidate for
operator data. ChaptarrNG `v0.9.977` remains the latest public release; no
release, package, image, or compatibility bridge was published during these
checks.

See [retirement status](../BOOKSHELFNG_RETIREMENT.md), the
[operator migration guide](../MIGRATE_FROM_BOOKSHELFNG.md), and the
[PostgreSQL candidate notes](../BOOKSHELFNG_POSTGRESQL_MIGRATION.md) for the
remaining acceptance gates.
