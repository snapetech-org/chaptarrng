# BookshelfNG migration fixture evidence

**Reviewed:** 2026-10-19. This file covers synthetic fixtures and one private
populated migration-158 snapshot tested on a disposable copy. This is
maintainer evidence, not approval to cut over operator data.

**Prior PR #131 application-code validation (2026-10-19):** Code snapshot `0612cb4c90b75818e5349eb868cc0638825de02a` passed exact-head Build [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373): Linux, macOS, and Windows tests, PostgreSQL migration smoke, native-runner receipt reconciliation, and the release-note contract all passed. CodeQL [39239934583](https://github.com/snapetech-org/chaptarrng/actions/runs/39239934583), security [39239940344](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940344), NuGet submission [39239932165](https://github.com/snapetech-org/chaptarrng/actions/runs/39239932165), and dependency review [39239940313](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940313) passed. Disposable-fork validation [39239940512](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940512) was skipped by its external-fork gate. SeerrNG's adapter and request-route suite at source commit `b810153c` passed 4 files / 283 tests. The Linux artifact also passed the earlier empty-library API and authorization smoke. The populated source-build check on later code is recorded below.

**SeerrNG pending-request lifecycle probe (2026-10-19):** On the isolated Linux candidate from Build run [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373), SeerrNG's real `ReadarrAPI` (`b810153c`) submitted an ebook request for a synthetic work whose author was unavailable. `addBook` received a pending ID, `getPendingAuthorImport` read it, `cancelPendingAuthorImport` cancelled it, and a second poll observed the expected terminal cancellation state: `OverallStatus=Failed` with `LastError=Cancelled by user`. The cancelled record remains queryable rather than being removed. This proves request creation, polling, and cancellation across the adapter/API boundary. It used a disposable database and synthetic IDs; provider-success behavior, retries, availability, history, migrated records, production settings, full SeerrNG operator workflow, and operator acceptance remain open. The tested app artifact is code snapshot `0612cb4c`; PR commit `d9713604` added the repeatable helper and docs, and the intervening app change is Calibre selector tie-breaking rather than a SeerrNG API change. The probe is now repeatable through `integration/seerrng-request-lifecycle.mjs` and is wired after the authorization smoke in `integration/seerrng-smoke.compose.yml`. On 2026-10-19, both scripts passed when run against the isolated artifact from Build 39239940373 and SeerrNG source `b810153c`; `node --check`, Prettier, and Compose configuration validation also passed. The earlier Compose invocation used a temporary runtime image with `--no-build`. A follow-up source-build run on 2026-10-19 built the candidate through the checked-in `Dockerfile.build` from PR head `7cfb5d87` and passed both scripts after the harness prepared its disposable `/books` volume as UID/GID `99:100`. Successful fulfillment, retries, availability/history, converted records, saved operator settings, and acceptance remain open.

## Candidate and fixture

The original populated fixture checkpoint also exercised provider settings
disposition. A matching Hardcover public-list provider was accepted; a Torznab
row with an unknown settings field and unsupported plugin providers were
reported as blockers. At that checkpoint, Goodreads OAuth bookshelf settings,
owned-book import lists, and Goodreads bookshelf/owned-book notifications were
also blockers. The later PR #131 candidate adds those Goodreads compatibility
providers. Importer tests verify complete saved settings are carried forward,
missing or partial OAuth credentials block migration, and reports never
include credential values. Runtime HTTP fixtures cover private-shelf and
owned-book reads plus Goodreads writes; existing-account API acceptance is
still unverified. The populated SQLite snapshot itself contains no real
Goodreads credentials or live provider response. Preview leaves source bytes
unchanged, and apply refuses any remaining unsupported provider before
promotion. Reports contain row IDs, implementation/contract names, and
unsupported field names, never saved values or credentials.

- ChaptarrNG source: draft PR [#131](https://github.com/snapetech-org/chaptarrng/pull/131). The populated rehearsal below is one fixture run; it does not stand in for representative-data acceptance.
- Earlier integrated code snapshot `8b4c6e23` passed Linux, macOS, Windows, PostgreSQL migration, release-note, and runner-receipt checks in Build run [39205452341](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452341). CodeQL, dependency review, security scan, and NuGet submission also passed. Those hosted checks do not rerun the private populated-data rehearsal or expand its scope.
- Earlier exact-head Build run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
  passed Linux, macOS, and Windows builds/tests, PostgreSQL migration fixtures,
  release-note validation, and runner-receipt reconciliation. CodeQL,
  dependency review, dependency/secret scans, and NuGet submission passed on
  the same head. Four Windows media-move fixtures failed in the preceding run
  because they used POSIX-only paths; the current fixtures use native temporary
  paths and pass. Disposable-fork validation was skipped by its gate.
- Documentation-only checkpoint `898b72fd` passed exact-head hosted run
  [39198708193](https://github.com/snapetech-org/chaptarrng/actions/runs/39198708193),
  including Linux, macOS, and Windows, PostgreSQL fixtures, release-note,
  CodeQL, dependency/security, NuGet, and runner-receipt checks. It did not
  change application code. PR #131 remains draft; these checks do not authorize
  operator cutover.
- Documentation-only checkpoint `ddec3e81` passed exact-head hosted run
  [39199730309](https://github.com/snapetech-org/chaptarrng/actions/runs/39199730309),
  including Linux, macOS, and Windows builds/tests, PostgreSQL, CodeQL,
  security/dependency, NuGet, release-note, and receipt checks. Disposable-fork
  validation was skipped by its gate. It did not change application code.
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
- **Current source-candidate smoke:** on 2026-10-18, the same harness passed
  against PR #131 source head `ddec3e8104f02fe15e1885c617cdb621e2f71fc5`
  (application-code snapshot `2c0401fc`) and SeerrNG source commit
  `10f06eab9`. Its isolated Compose run verified capability discovery, the
  empty paged library, and denial of administrative and unrelated command
  routes to the restricted service key. It does not establish populated
  migration acceptance or complete SeerrNG workflows.
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

## Legacy BookshelfNG backup ZIP restore candidate

Draft PR #131 adds source-lineage validation that allows the standard restore
flow to stage a recognized BookshelfNG database extracted from a legacy
`readarr_backup_*.zip`. Normal startup then upgrades a disposable copy and
converts it through the existing row-accounted workflow; the archive remains
untouched and the BookshelfNG source database is retained by the converter for
rollback. The previous ChaptarrNG database in `.pre-restore` is removed after
successful migration and restored if conversion fails. A regression fixture
covers restore staging and automatic conversion from an authentic schema-158
fixture. The local test run is blocked before compilation by the machine's
missing ImageSharp license; hosted exact-head validation passed on code head
`6f57d355` in
[run 39211491613](https://github.com/snapetech-org/chaptarrng/actions/runs/39211491613).
Linux reported 4,363 passed, macOS 4,360 passed, and Windows 4,326 passed, with
zero failures on all three platforms. PostgreSQL migration checks passed 4/4.
The first run failed because the synthetic schema-158 restore fixture contained
only its maximum `VersionInfo` row; the migration runner expects the complete
applied-version history. Both restore fixtures now include versions 1 through
158, matching the existing migration fixtures. This is not operator-data
acceptance, and the published `.977` restore path remains unchanged.

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
parity, and installer/update-channel acceptance remain open. The earlier populated fixture validation used code snapshot `2c0401fc` and passed
exact-head hosted run [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646).
An earlier integrated code snapshot `8b4c6e23` passed cross-platform and
PostgreSQL CI in run [39205452341](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452341),
but the private populated fixture was not rerun in that workflow. The source
changes remain unpublished, and operator-data, feature-parity,
SeerrNG workflow, and installation-channel acceptance remain open.

## Populated migration-to-SeerrNG source build (2026-10-19)

ChaptarrNG candidate commit `12f290bd` was built from source with the checked-in
`Dockerfile.build`. The Compose harness seeded a disposable named config volume
with the shared populated BookshelfNG migration-158 fixture used by
`BookshelfDatabaseImporterFixture`, plus synthetic `.m4b` and `.epub`
placeholder files in a disposable named library volume. It then started
ChaptarrNG normally, exercising startup conversion rather than a direct test
helper. The target reached schema 121 and the original schema-158 database was
retained as the migration backup. Both databases passed integrity and foreign
key checks. With `HARDCOVER=true`, the target config included
`HardcoverEnabled=true`.

SeerrNG's `ReadarrAPI` at commit `fdc439aa9` read the converted audiobook
`Fixture Audiobook` and ebook `Fixture Book` through the format-specific
facades and checked their Hardcover book identities. The same run exercised
capability discovery, the restricted-key boundary, and the pending ebook
add/poll/cancel lifecycle. It reported ChaptarrNG `0.9.978.0`. The importer
suite passed 43/43; Compose config validation, `node --check`, and Prettier
checks passed. The shared fixture avoids separate, drifting seed data between
the unit tests and integration smoke.

The media files are deliberately tiny placeholders and are not valid M4B or
EPUB samples. Separately, the six-case real-tool importer smoke passed on
candidate `12f290bd` with generated MP3 input and the pinned `m4b-tool` 0.5.2 /
`mp4v2` 2.1.1 toolchain; it checks M4B creation and fallback but does not
represent operator media or embedded source chapters. That test is now run by
the Linux build workflow. Real provider responses, operator records, pre-14
schemas, PostgreSQL operator data, successful SeerrNG fulfillment, installed
rollback, and update channels remain unvalidated. It is local synthetic
evidence; no public image, release, package, or channel alias was published.
PR #131 remains draft and the release freeze remains active.
