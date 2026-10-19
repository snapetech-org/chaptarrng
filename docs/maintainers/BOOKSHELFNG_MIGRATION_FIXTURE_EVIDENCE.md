# BookshelfNG migration fixture evidence

**Reviewed:** 2026-10-19. This file covers synthetic fixtures and one private
populated migration-158 snapshot tested on a disposable copy. This is
maintainer evidence, not approval to cut over operator data.

## Candidate and fixture

- ChaptarrNG source: draft PR [#131](https://github.com/snapetech-org/chaptarrng/pull/131), current head 2c0401fc.
- Exact-head Build run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
  passed Linux, macOS, and Windows builds/tests, PostgreSQL migration fixtures,
  release-note validation, and runner-receipt reconciliation. CodeQL,
  dependency review, dependency/secret scans, and NuGet submission passed on
  the same head. Four Windows media-move fixtures failed in the preceding run
  because they used POSIX-only paths; the current fixtures use native temporary
  paths and pass. Disposable-fork validation was skipped by its gate. PR #131
  remains draft; these checks do not authorize operator cutover.
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
At the time of this 2026-10-18 rehearsal, these changes were source-only and
exact-head hosted checks were still pending. The later exact-head result is
recorded below. This rehearsal used a synthetic fixture and does not establish
behavior on operator data or validate the Unraid template handoff.

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

## Exact-head provider-mode validation (2026-10-18)

ChaptarrNG PR #131 head `e4db2d461773ea59cc57440d1226fcfcf24ccb58` passed
[exact-head run 39175250100](https://github.com/snapetech-org/chaptarrng/actions/runs/39175250100).
GitHub validated PR merge candidate `724029a27e37458994ee3e5d3d9c4601f3d1c5a2`.
Platform receipts reported:

- Linux: 4,341 passed, 0 failed, 4 skipped, 2 inconclusive.
- macOS: 4,338 passed, 0 failed, 7 skipped, 2 inconclusive.
- Windows: 4,304 passed, 0 failed, 10 skipped, 33 inconclusive.
- PostgreSQL migration fixture: 4/4 cases passed, including normal-startup
  conversion and backup retention.
- Release-note validation and native-runner receipt reconciliation passed.
  C#, JavaScript/TypeScript, Python, and Actions CodeQL, dependency review,
  secret scan, and NuGet submission checks passed. The disposable-fork check
  was skipped by its pull-request policy.

The Linux artifact reports ChaptarrNG `0.9.978.0`; its `Chaptarr.Core.dll`
SHA-256 is
`74089c13ebfa844acb8363a72cf8fc634fc04e5f73d0945fc8d40a785c308e4a`. I built
that artifact into an isolated local ASP.NET runtime image and tested three
copies of the populated synthetic migration-158 database created by the
published BookshelfNG `.71` binary. The original source database hash was
`9861ec1bb7d940841b564a00000ef35de43cac7a84b85ad1b733e1f2824d3ab0`.

- With `HARDCOVER` unset, preview exited with the explicit fail-closed message;
  the source hash and schema version 158 remained unchanged.
- With `HARDCOVER=false`, preview left the source hash unchanged and apply
  mapped author/book/edition/series IDs into Goodreads fields. The converted
  database reached schema 121 and passed SQLite integrity and foreign-key
  checks.
- With `HARDCOVER=true`, preview and apply preserved author `hc:9001`, book
  `hc:9002`, editions `9003`/`9004`, and series `hc:9005` in Hardcover fields.
  Normal startup also converted a separate copy, served `/ping`, returned one
  author and two books across ebook and audiobook formats, retained the source
  backup, and passed integrity and foreign-key checks.
- Restore preview left the converted database unchanged. Explicit rollback
  restored the retained BookshelfNG snapshot byte-for-byte (SHA-256
  `f59219f827571eb7111b3a2067d708e4193dc338e3dce9fffbf1d10d27c6e273`),
  returned to schema 158, and preserved the ChaptarrNG schema-121 backup
  (SHA-256
  `ab57924a7662bbf7a8184b8627ebab9d0d18f53c18d8c693f4093d9d184b4301`).
  Integrity and foreign-key checks passed after restore.

These are hosted candidate checks and local synthetic rehearsals. They do not
establish representative operator-data acceptance, review of ambiguous media
choices, provider API parity, PostgreSQL operator-data rollback, or installer
and update-channel acceptance. ChaptarrNG `v0.9.977` and BookshelfNG `0.4.21.71`
remain the latest public releases; no image, package, release, or compatibility
alias was published or advanced during these checks.

## Populated private SQLite preview, apply, and restore rehearsal (2026-10-19)

An unpublished PR #131 candidate build reporting ChaptarrNG `0.9.978.0` was
tested against a disposable copy of a private populated BookshelfNG
migration-158 SQLite snapshot. The test ran with `HARDCOVER=true`, no network
access, a writable isolated config copy, and a read-only candidate app mount.
The live BookshelfNG service and the original snapshot were not changed.

- **Preview:** migration preview passed; the source snapshot remained
  byte-identical.
- **Apply:** explicit apply passed with stopped-service confirmation. The
  converted ChaptarrNG database reached schema 121 and passed SQLite
  `integrity_check` with zero foreign-key errors. The retained BookshelfNG
  backup remained schema 158 and also passed integrity and foreign-key checks.
- **Restore preview:** passed without changing either the converted database
  or the source backup.
- **Restore:** explicit rollback restored a byte-identical copy of the
  retained BookshelfNG backup and preserved the converted schema-121 database
  as a separate ChaptarrNG backup. The restored source reached schema 158; the
  independent original snapshot hash was unchanged.

The populated source exposed two inconsistent relationship shapes. Files
whose positive `EditionId` did not exist are now preserved as unmapped, with
no guessed book or media type. History rows whose source book disagrees with
the referenced file remain attached to the source book, preserving their
payload while leaving the unsafe file and edition association empty. Ambiguous
multiple-file history matches also remain unlinked and are reported instead
of aborting conversion. Focused regression cases cover these paths.

The focused `BookshelfDatabaseImporterFixture`,
`EditionCatalogResourceFixture`, and
`SystemIntegrationCapabilitiesResourceFixture` suites pass 52/52 locally.
The private library's contents, counts, identifiers, and hashes are omitted.
This validates one populated SQLite candidate path and its rollback, not
semantic correctness for every record. The ambiguous-media review artifact
still requires operator classification. Older real schemas, PostgreSQL
operator data, complete settings/provider and client continuity, feature
parity, and installer/update-channel acceptance remain open. Exact-head hosted validation passed in run
[39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646).
The source changes remain unpublished, and operator-data, feature-parity,
SeerrNG workflow, and installation-channel acceptance remain open.
