# BookshelfNG retirement status

**Reviewed:** 2026-10-19 after BookshelfNG `main` added the SeerrNG edition
catalog endpoint and the ChaptarrNG candidate passed a populated SQLite
preview/apply/restore rehearsal. ChaptarrNG `v0.9.977` remains the latest
published release; PR #128 merged the fail-closed release and distribution
gates. This page is the current
handoff summary for moving BookshelfNG users to ChaptarrNG. The
detailed feature inventory remains in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).

**Latest application-code validation (2026-10-19):** ChaptarrNG draft PR #131
contains code commit `0e881d921dd55aa89e12747557ccaadbb2352a21`. Exact-head Build
[39313568727](https://github.com/snapetech-org/chaptarrng/actions/runs/39313568727)
passed Linux (4,460 passed, 0 failed, 13 skipped, 2 inconclusive), macOS
(4,455 passed, 0 failed, 18 skipped, 2 inconclusive), and Windows (4,421
passed, 0 failed, 21 skipped, 33 inconclusive) suites. PostgreSQL migration,
release-note, native-runner receipt, and Linux pinned-tool M4B checks passed.
CodeQL [118269449163](https://github.com/snapetech-org/chaptarrng/runs/118269449163),
security/dependency scan
[39313569305](https://github.com/snapetech-org/chaptarrng/actions/runs/39313569305),
dependency review
[39313568981](https://github.com/snapetech-org/chaptarrng/actions/runs/39313568981),
NuGet submission
[39313556294](https://github.com/snapetech-org/chaptarrng/actions/runs/39313556294),
and Helm resource-identity validation
[39313569136](https://github.com/snapetech-org/chaptarrng/actions/runs/39313569136)
passed. Disposable-fork validation
[39313569934](https://github.com/snapetech-org/chaptarrng/actions/runs/39313569934)
was skipped by its external-fork gate.

The exact-head Linux artifact also passed a populated synthetic migration and
SeerrNG smoke against SeerrNG `86c6908e53777fd6cdab525bab56abf27176e3b4`:
normal startup converted the migration-158 fixture; SeerrNG read both migrated
formats and their Hardcover identities; the restricted key was denied
administrative and unrelated command routes; and the real `ReadarrAPI` created,
polled, and cancelled a synthetic pending request. See the detailed evidence
below. This does not close successful provider fulfillment, representative
operator data, semantic parity, real media/M4B acceptance, installed rollback,
installer/channel handoff, or operator sign-off. No release or bridge was
published.

The Windows standalone handoff is still open. BookshelfNG's WPF installer
continues to install its own `Readarr` service. Draft PR #131 now includes a
ChaptarrNG-owned, migration-only WPF installer candidate for one SQLite
database using the standard `NT AUTHORITY\LocalService` account. It detects the
legacy data path, asks for provider mode, makes a retained integrity-checked
backup, checks preview immutability across SQLite sidecars, previews before
apply, switches to the `Chaptarr` service, and verifies its data path and local
health. Custom service accounts, PostgreSQL, and multiple databases fail
closed. Its Windows CI build and exact-head checks are pending; representative
Windows data and real installed rollback remain unaccepted. Keep the release
freeze in place.

**Exact-head artifact-based migration-to-SeerrNG smoke (2026-10-19):** The
Linux app files from Build [39313568727](https://github.com/snapetech-org/chaptarrng/actions/runs/39313568727)
were run with the checked-in Compose fixture against disposable config and
library volumes. SeerrNG `ReadarrAPI` at `86c6908e53777fd6cdab525bab56abf27176e3b4`
read the migrated synthetic audiobook and ebook; the restricted key accessed
capabilities and both libraries while requests to `/config/host` and
`/command` were denied. The SeerrNG adapter created a synthetic unavailable-
author request, polled it, cancelled it, and observed the terminal
`Failed` / `Cancelled by user` state. The ChaptarrNG candidate reported
`0.9.978.0`. The test used disposable synthetic records and did not exercise
successful provider fulfillment, representative user data, full SeerrNG
history/availability workflows, or installed rollback. Test containers,
images, and volumes were removed after the run.

The diagnostics handoff now has startup-path fixtures for default-off behavior,
legacy opt-in only with the archived `Bookshelf.Diagnostics.dll` marker,
explicit ChaptarrNG enable/disable precedence, and a missing replacement module.
Existing reporter fixtures cover the aggregate event payload and legacy
endpoint/token fallback. These tests verify source-level selection; installed
package environment transfer remains open.

**Prior PR #131 application-code validation (2026-10-19):** Code snapshot `0612cb4c90b75818e5349eb868cc0638825de02a` passed exact-head Build [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373): Linux, macOS, and Windows tests, PostgreSQL migration smoke, native-runner receipt reconciliation, and the release-note contract all passed. CodeQL [39239934583](https://github.com/snapetech-org/chaptarrng/actions/runs/39239934583), security [39239940344](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940344), NuGet submission [39239932165](https://github.com/snapetech-org/chaptarrng/actions/runs/39239932165), and dependency review [39239940313](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940313) passed. Disposable-fork validation [39239940512](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940512) was skipped by its external-fork gate. SeerrNG's adapter and request-route suite at source commit `b810153c` passed 4 files / 283 tests. The Linux artifact also passed the earlier empty-library API and authorization smoke. The populated source-build check on later code is recorded below.


**Prior candidate code CI (2026-10-19):** ChaptarrNG PR #131 head `38be9422a0390c0b3d4d1c453ec14b25d3aa99a4` includes portable BookshelfNG BKSENC01 `.enc` backup export and restore. After two compile fixes and a cleanup-callback test-fixture correction, exact-head Build [39267876585](https://github.com/snapetech-org/chaptarrng/actions/runs/39267876585) passed Linux, macOS, Windows, PostgreSQL migration, release-note, and native-runner receipt checks. CodeQL [118112421054](https://github.com/snapetech-org/chaptarrng/runs/118112421054), security analysis [39267876503](https://github.com/snapetech-org/chaptarrng/actions/runs/39267876503), and dependency review [39267876754](https://github.com/snapetech-org/chaptarrng/actions/runs/39267876754) passed. Disposable-fork validation was skipped by its external-fork gate. This is code validation only; migration, parity, installed rollback, update-channel, and operator-acceptance gates remain open.

**M4B fix and previous exact-head validation (2026-10-19):** PR #131 commit
`c4fa8125095f583ad9eee534f70328e8eef1b44c` fixes a Move-mode defect found by
embedded-chapter tests: after a profile-based multipart M4B merge succeeded,
the importer kept its source tracks. Fixture isolation alone did not fix it;
Builds [39275841754](https://github.com/snapetech-org/chaptarrng/actions/runs/39275841754)
and [39287179772](https://github.com/snapetech-org/chaptarrng/actions/runs/39287179772)
still failed the Move cleanup check. Exact-head Build
[39288736177](https://github.com/snapetech-org/chaptarrng/actions/runs/39288736177)
passed Linux's full suite and pinned-tool M4B checks, PostgreSQL migration
smoke, and release-note contract. Windows build and full test steps passed. The macOS job was cancelled before a runner started, so the cross-platform matrix is incomplete. CodeQL
[39288728262](https://github.com/snapetech-org/chaptarrng/actions/runs/39288728262),
security analysis
[39288735208](https://github.com/snapetech-org/chaptarrng/actions/runs/39288735208),
dependency review
[39288734998](https://github.com/snapetech-org/chaptarrng/actions/runs/39288734998),
and NuGet submission
[39288748702](https://github.com/snapetech-org/chaptarrng/actions/runs/39288748702)
passed; disposable-fork validation was skipped by its gate. Representative
M4B media, restart/resume, and operator acceptance remain open. No release or
bridge was published.

**Latest passed PR #131 application-code validation (2026-10-19):** Code commit
`b7e900d49648269d5118bbfba44289c0200b095b` passed Build
[39292223717](https://github.com/snapetech-org/chaptarrng/actions/runs/39292223717):
Linux, macOS, and Windows suites, PostgreSQL migration smoke, release-note
contract, and native-runner receipt reconciliation passed. Linux also passed
the pinned-tool M4B merge and fallback smoke. CodeQL
[118195871399](https://github.com/snapetech-org/chaptarrng/runs/118195871399),
security and secret scan
[39292223457](https://github.com/snapetech-org/chaptarrng/actions/runs/39292223457),
dependency review
[39292223396](https://github.com/snapetech-org/chaptarrng/actions/runs/39292223396),
and NuGet submission
[39292213603](https://github.com/snapetech-org/chaptarrng/actions/runs/39292213603)
passed. Disposable-fork validation was skipped by its external-fork gate.
This validates application code only. PR #131 remains draft; see its
[live checks](https://github.com/snapetech-org/chaptarrng/pull/131) for the
current head. It does not establish retirement acceptance; representative
data, semantic parity, installed rollback, and channel handoff remain open.

**Candidate code CI (2026-10-19):** ChaptarrNG PR #131 code commit `12f290bd` passed [Build 39256254892](https://github.com/snapetech-org/chaptarrng/actions/runs/39256254892): Linux, macOS, Windows, PostgreSQL migration, release-note, and native-runner receipt checks passed. CodeQL, [security scan 39256254713](https://github.com/snapetech-org/chaptarrng/actions/runs/39256254713), [dependency review 39256254710](https://github.com/snapetech-org/chaptarrng/actions/runs/39256254710), and [NuGet submission 39256277391](https://github.com/snapetech-org/chaptarrng/actions/runs/39256277391) passed. Disposable-fork validation was skipped by its external-fork gate. This is source validation, not migration or release acceptance.

**SeerrNG pending-request lifecycle probe (2026-10-19):** On the isolated Linux candidate from Build run [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373), SeerrNG's real `ReadarrAPI` (`b810153c`) submitted an ebook request for a synthetic work whose author was unavailable. `addBook` received a pending ID, `getPendingAuthorImport` read it, `cancelPendingAuthorImport` cancelled it, and a second poll observed the expected terminal cancellation state: `OverallStatus=Failed` with `LastError=Cancelled by user`. The cancelled record remains queryable rather than being removed. This proves request creation, polling, and cancellation across the adapter/API boundary. It used a disposable database and synthetic IDs; provider-success behavior, retries, availability, history, migrated records, production settings, full SeerrNG operator workflow, and operator acceptance remain open. The tested app artifact is code snapshot `0612cb4c`; PR commit `d9713604` added the repeatable helper and docs, and the intervening app change is Calibre selector tie-breaking rather than a SeerrNG API change. The probe is now repeatable through `integration/seerrng-request-lifecycle.mjs` and is wired after the authorization smoke in `integration/seerrng-smoke.compose.yml`. On 2026-10-19, both scripts passed when run against the isolated artifact from Build 39239940373 and SeerrNG source `b810153c`; `node --check`, Prettier, and Compose configuration validation also passed. The earlier Compose invocation used a temporary runtime image with `--no-build`. A follow-up source-build run on 2026-10-19 built the candidate through the checked-in `Dockerfile.build` from PR head `7cfb5d87` and passed both scripts after the harness prepared its disposable `/books` volume as UID/GID `99:100`. Successful fulfillment, retries, availability/history, converted records, saved operator settings, and acceptance remain open.

**Populated migration-to-SeerrNG source smoke (2026-10-19):** Candidate
`12f290bd` built through `Dockerfile.build`, started with a shared synthetic
BookshelfNG migration-158 fixture, and converted it during normal startup.
The target reached schema 121, retained its schema-158 source backup, and
persisted `HardcoverEnabled=true` for the source `HARDCOVER=true` mode.
SeerrNG `ReadarrAPI` commit `fdc439aa9` read both migrated audiobook and ebook
records with their Hardcover identities. Capability discovery, restricted
service-key authorization, and pending add/poll/cancel also passed. The
focused importer suite passed 43/43. This is populated synthetic integration
evidence, not a private operator-data rehearsal. The `.m4b`/`.epub` files in
this migration fixture are placeholders. A separate real-tool importer check
passed six synthetic multipart M4B cases, including merge metadata, chapter
ordering, Move/Copy handling, and failure fallback. PR #131 adds synthetic embedded-chapter Move/Copy cases and a TagLib write/reprobe check. They passed on c4fa in exact-head Linux Build 39288736177. Representative audiobooks and
restart/resume remain unvalidated. The older empty-library check is superseded
only for this synthetic conversion path;
provider success, older schemas, PostgreSQL operator data, full fulfillment,
installed rollback, operator sign-off, feature parity, and channel handoff
remain open. See the [maintainer fixture report](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md)
and [SeerrNG integration guide](SEERRNG_INTEGRATION.md).

## Current releases

### Provider configuration blockers

The migration candidate preserves provider settings only when a matching
ChaptarrNG implementation, settings contract, and populated saved fields exist.
It includes the legacy Hardcover public-list and reading-status provider and
now ports BookshelfNG's OAuth-backed Goodreads bookshelf and owned-book lists
and bookshelf/owned-book write notifications. The importer preserves complete
saved credentials and blocks missing or partial OAuth settings without showing
their values. Public Goodreads shelves remain available without OAuth when a
valid user ID is saved. Goodreads API acceptance with existing credentials is
still unverified, and no new developer keys are available; this remains a
feature-parity and migration-readiness gate. See the
[migration guide](MIGRATE_FROM_BOOKSHELFNG.md#provider-configuration-blockers)
and [Goodreads API deprecation notice](https://www.goodreads.com/topic/show/21788520-api-deprecation).

The candidate ports BookshelfNG's legacy `AudiobookshelfMetadata` OPF
consumer as ChaptarrNG's distinct `LegacyAudiobookshelfMetadata` provider.
Provider and sidecar IDs are mapped while preserving the exact `NullConfig`
contract and file/book references. Unknown metadata providers or sidecar
consumers block apply. Representative OPF libraries and live migration
acceptance remain open.

- ChaptarrNG `v0.9.977` is published with the standard image, migration tools,
  the embedded BookshelfNG SQLite migration chain, and standalone release
  archives.
- The published `.977` converter still rejects source schemas before
  migration 14 because they contain `Blacklist`, which the embedded chain
  later renames to `Blocklist`. PR #126 merged the version-aware guard and
  authentic migration-1 coverage to `main`; hosted Linux, macOS, Windows, and
  PostgreSQL migration checks passed. This fix is source-only until a later
  ChaptarrNG release.
- ChaptarrNG releases are frozen while retirement gates remain open. The
  merged release and distribution workflows require
  `CHAPTARRNG_RELEASE_READY=true`; this repository variable remains unset.
  Do not set it until parity, migration/rollback, and channel acceptance are
  complete. The current `main` version bump to `.978` is source-only.
- ChaptarrNG PR #127 merged to `main` as `b3089a39`, adding selectable
  supplemental catalogs, per-field preferences, and conservative
  cross-catalog unmapped-file recovery through exact ISBN, ASIN, provider ID,
  or normalized title/author evidence. It remains source-only and is not
  available in `.977`; converted-data and settings acceptance remain open.
- ChaptarrNG PR #129 merged to `main` as `949ae3dd`, adding opt-in BookshelfNG
  M4B merge settings, tagged track ordering and chapter titles, original-track
  fallback, and safe move/copy source handling. Hosted Linux, macOS, Windows,
  PostgreSQL migration, CodeQL, dependency/security, and release-note checks
  passed on its clean head. The retirement candidate passed a
  tool-enabled synthetic two-track importer smoke using the real converter; it
  verifies track order, chapter titles and offsets, metadata, one registered
  file, Move-mode source cleanup, and Copy-mode source retention. Focused
  importer tests also cover merge selection and fallback with a mocked
  converter. The six-case real-tool smoke also verifies fallback to separate
  original-track imports in Move and Copy modes when one source cannot be
  probed, and when `m4b-tool` fails after a controlled FFmpeg conversion error.
  Synthetic embedded-chapter Move/Copy and TagLib-write cases pass on c4fa; representative M4B media and restart or resume remain open. This source is not in `.977`.
- The current retirement candidate is draft ChaptarrNG
  [PR #131](https://github.com/snapetech-org/chaptarrng/pull/131). It adds
  older SQLite support, one migration-158 PostgreSQL source schema, native
  Hardcover identity handling, and BookOrbit Book Dock as a separate
  destination. Previous exact-head run
  [39182438062](https://github.com/snapetech-org/chaptarrng/actions/runs/39182438062)
  passed on PR head `bdd7518`; it validated the candidate before the
  populated-data importer fixes and edition-catalog addition described below.
  Code snapshot `2c0401fc` passed exact-head run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646),
  and docs-only checkpoint `ddec3e81` passed run
  [39199730309](https://github.com/snapetech-org/chaptarrng/actions/runs/39199730309).
  These checks do not close operator-data, feature-parity, installed-rollback,
  or channel acceptance.
  The candidate now also validates and stages recognized BookshelfNG ZIP
  backup databases for normal-startup conversion, retaining the untouched
  archive and original BookshelfNG database for rollback. The previous
  ChaptarrNG database is restored if conversion fails; its temporary
  `.pre-restore` copy is removed after success. The first hosted run exposed an
  incomplete migration history in the synthetic fixture; the corrected
  fixture matches schema 158. Exact-head Build run
  [39211491613](https://github.com/snapetech-org/chaptarrng/actions/runs/39211491613)
  passed on code head `6f57d355`: Linux, macOS, Windows, PostgreSQL migration,
  and release-note checks passed. CodeQL, dependency review, dependency/secret
  scan, and NuGet checks also passed. Local tests remain blocked before
  compilation by the workstation's ImageSharp license requirement. This
  source change is not in public `.977`, which still rejects BookshelfNG ZIPs.
  The earlier synthetic provider-mode, startup, and rollback evidence is in the
  [detailed fixture report](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md).
  This is not operator-data or channel acceptance.
- On 2026-10-19, the unpublished PR #131 candidate (build version
  `0.9.978.0`) passed preview, explicit apply, rollback preview, and explicit
  rollback against a task-scoped copy of populated migration-158 operator data
  with `HARDCOVER=true`. The live BookshelfNG service remained running, and
  candidate commands targeted only the separate copy; this did not test an
  installed service handoff. Migration preview left the source copy unchanged;
  apply produced schema 121 and retained the source. Restore preview left the
  converted database and source backup unchanged; rollback restored a
  byte-identical schema-158 backup and preserved the converted database
  separately. Integrity and foreign-key checks passed, and the original
  private snapshot remained unchanged. Unresolved books were retained in both
  catalogs as unmonitored and recorded in the review sidecar; their media types
  still need human review. Configured provider rows in the snapshot passed
  migration compatibility checks without blockers; this does not test live
  provider calls. With native Hardcover mode enabled, every migrated author,
  book, and edition retained its Hardcover identity; none were assigned
  Goodreads IDs. A fresh copy of the restored database also opened in the
  BookshelfNG `hardcover` image on Docker's isolated `none` network, and its
  configured `/ping` endpoint returned HTTP 200. This checks database reopen
  and startup health only, not complete library behavior or an installed image
  switch. The `.978` Linux app artifact from Build
  [39267876585](https://github.com/snapetech-org/chaptarrng/actions/runs/39267876585)
  also returned HTTP 200 from `/ping` against a fresh converted schema-121 copy,
  running on the published `.977` runtime image with empty scratch mounts for
  configured roots and an internal-only network. This checks candidate app
  startup, not candidate image packaging or live image switching. The importer
  fixes prompted by this rehearsal preserve
  missing-edition files as unmapped and retain
  conflicting history on its source book without attaching an unsafe file or
  edition. Focused importer, edition-catalog, and capability tests pass 52/52
  locally. Exact-head run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
  passed on earlier PR #131 head 2c0401fc: Linux, macOS, and Windows build/tests,
  PostgreSQL migrations, release-note validation, CodeQL, dependency and
  secret checks, NuGet submission, and runner-receipt reconciliation. The
  preceding run [39195460843](https://github.com/snapetech-org/chaptarrng/actions/runs/39195460843)
  exposed four Windows fixtures using POSIX-only paths; the follow-up uses
  native temporary paths and passes. Disposable-fork validation was skipped
  by its gate. This validates hosted candidate code, not operator cutover.
  Ambiguous format review, PostgreSQL operator data, other source shapes,
  complete feature parity, installed rollback, and channel acceptance remain
  open.
- BookshelfNG `main` at `6e1a62e12` now exposes
  `GET /api/v1/book/{id}/editions/catalog` and advertises `editionCatalog` for
  SeerrNG edition cards. ChaptarrNG `main` now includes the matching route and
  capability, an **Editions and Narrations** book-details tab, and a series
  narrator comparison. Unstated details remain visibly unprovided.
  BookshelfNG source commit `6e1a62e12` passed hosted CI, release-note
  validation, and CodeQL; the manual run skipped release and image-publication
  jobs. The source change is not a new release.
  Earlier candidate code snapshot `8b4c6e23` integrated then-current ChaptarrNG `main`,
  including its Editions tab and series narrator comparison, and retains the
  edition labels for unknown and not-applicable metadata. Local translation,
  type, targeted ESLint, and Stylelint checks pass. Exact-head Build run
  [39205452341](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452341)
  passed Linux, macOS, Windows, PostgreSQL migration, release-note, and runner-
  receipt checks. CodeQL [39205449712](https://github.com/snapetech-org/chaptarrng/actions/runs/39205449712),
  dependency review [39205452346](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452346),
  security scan [39205452337](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452337),
  and NuGet submission [39205449409](https://github.com/snapetech-org/chaptarrng/actions/runs/39205449409)
  also passed. Disposable-fork validation was skipped by its gate. The workflow
  produced Actions validation artifacts only; it did not publish a release or
  package. The route and catalog implementation at code snapshot `2c0401fc`
  passed run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646).
  An earlier source candidate also passed the isolated SeerrNG Compose smoke
  against PR #131 head `ddec3e81` and SeerrNG `10f06eab9`: capability discovery,
  empty paged-library access, and restricted-key denials passed. This does not
  establish populated-data acceptance or end-to-end SeerrNG workflows.
  Unchanged SeerrNG edition-card, request, polling, availability, pending-work,
  and authorization acceptance remains open.
- This work branch also ports the native Hardcover metadata client and routes `hc:`/legacy Hardcover work, author, and edition IDs to it. The importer maps a saved `HardcoverAuth` value to ChaptarrNG's `HardcoverApiToken` setting; environment credentials remain external to the database. Bare numeric source IDs now require an explicit mode: `HARDCOVER=true` (unless `HARDCOVER_NATIVE=false`) selects Hardcover, and `HARDCOVER=false` selects Goodreads. An unset or invalid mode fails closed. The Docker candidate builds separate softcover and Hardcover compatibility targets so legacy aliases can retain that mode; `latest`, `main`, and `hardcover` must use the Hardcover target, while `softcover` must use the Goodreads target. These source changes are not in `.977`, PR #131 remains draft, and converted-data acceptance remains open.
- BookshelfNG's BookLore notification settings and `OnReleaseImport` trigger are copied unchanged into ChaptarrNG, and remain in the retained source backup. A separate BookOrbit Book Dock destination is candidate work in this branch; it does not rewrite BookLore URLs or credentials. Mock tests cover login, Book Dock permission, streamed uploads, redirects, rejected credentials/files, and partial-batch failure. An isolated BookOrbit v3.2.0 live API check passed setup, login, permission check, EPUB upload/listing, and unsupported-file rejection. That does not verify downstream processing of a production book or a real-user handoff. The integration is not in `.977`, and the broader retirement gate remains open.
- Draft PR #131 adds ChaptarrNG's format-aware author-media move workflow:
  preview and confirm a single-author or bulk move, move only registered media
  and linked book sidecars, check destination conflicts and copy space, and
  leave unknown files and author-wide extras in place. The UI requires a
  reviewed preview token before queueing, the generic command endpoint rejects
  direct move commands, and the worker stops if a source changes after preview.
  Sidecars resolve against the linked book's format root, retain paths relative
  to the destination format root, and fail closed when a registered relative
  path escapes the author folders. Focused tests cover format isolation,
  shared-root and format-root sidecars, path traversal, untracked files,
  conflicts, stale sources, and bulk preview. Run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
  passed on earlier head 2c0401fc, including Windows tests after replacing the
  POSIX-only fixture paths identified by run
  [39195460843](https://github.com/snapetech-org/chaptarrng/actions/runs/39195460843).
  This implementation is not on main or in .977; representative library,
  cross-device, partial-failure rollback, and channel acceptance remain open.
- BookshelfNG `0.4.21.71` is published as the final source release.
- The BookshelfNG `.71` tag contains issue #114's optional new-author path
  setting. ChaptarrNG has equivalent new-author path initialization in
  `v0.9.974` and later. ChaptarrNG applies configured format defaults
  automatically; it has no separate toggle. Explicit author roots are kept,
  ambiguous or missing defaults stay blank, and existing authors or files are
  not changed.

## Database migration contract

**Current operator decision: preview only.** The published `.977` converter
can assess the exact migration-158 SQLite schema without changing the source,
but applying migration or starting ChaptarrNG against BookshelfNG appdata is
not approved until data, parity, rollback, and channel acceptance close.
Normal ChaptarrNG startup automatically converts a supported source database,
so keep BookshelfNG in service and do not point a ChaptarrNG install at its
appdata yet.

The published ChaptarrNG `v0.9.977` converter accepts the BookshelfNG
migration-158 SQLite database and bundles the BookshelfNG SQLite migration
chain. It upgrades a disposable copy before import and retains the untouched
snapshot as the rollback source. **Do not use `.977` for native Hardcover
source databases:** its importer maps bare numeric IDs to Goodreads even when
the source used `HARDCOVER=true`; integrity checks and read-only preview do not
detect that identity error. The unpublished candidate requires explicit
provider mode. The `.977` converter still rejects versions
1–13 at its pre-upgrade table check; those versions retain `Blacklist` until
the embedded chain renames it. PR #126 merged the version-aware guard and
synthetic migration-1 rollback coverage into `main`, and its hosted migration
checks passed on Linux, macOS, Windows, and PostgreSQL. Neither the source fix
nor synthetic fixtures establish behavior on real older-schema operator data
or every historical release. Schema 159 is rejected without changing the
source. PostgreSQL source databases and merging two populated BookshelfNG
databases from one appdata directory remain unsupported by published
ChaptarrNG releases. ChaptarrNG refuses multiple non-empty legacy candidates
instead of guessing or merging. Independently operated BookshelfNG instances
can be migrated one at a time with separate appdata directories; co-located
databases need their ownership and settings resolved before separate copies can
be migrated. A PostgreSQL
migration-158 candidate is under review in this work branch; it does not
support older PostgreSQL schemas. The candidate remains unaccepted for operator data; see the [PostgreSQL migration notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md). A private populated migration-158 SQLite snapshot completed preview, apply, and rollback with public `.977` on Linux/amd64 on 2026-10-18. That proved structural conversion and rollback only; it did not validate native Hardcover identity. The separate 2026-10-19 candidate rehearsal with explicit Hardcover mode passed the same four-step path on a disposable copy and restored the source database byte-for-byte from its retained backup. Neither run resolves ambiguous media classifications, PostgreSQL acceptance, feature parity, or installation-channel handoff; details are in the [operator guide](MIGRATE_FROM_BOOKSHELFNG.md).
The SQLite path stages a native database, accounts for source rows, checks
SQLite integrity and foreign keys, and promotes only after validation. Preview
does not modify the source. Apply and rollback are explicit operations, and
normal startup can run the same converter when it finds a supported source
database. The work-branch PostgreSQL candidate uses temporary schemas and
transactional promotion, checks row accounting and both schema lineages, and
retains the original tables in a backup schema. The exact-head PostgreSQL
fixture passed all 4 migration cases in run
[39175250100](https://github.com/snapetech-org/chaptarrng/actions/runs/39175250100),
including rollback and normal-startup conversion with backup retention; this
remains synthetic evidence. Operator-data acceptance remains open; see the
[PostgreSQL migration notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md).

Some BookshelfNG books do not contain enough data to infer ebook versus
audiobook. Preview writes a private review file. Operators can provide a
complete, source-matched media map before conversion; with no map, conversion
keeps each unresolved work in both ChaptarrNG catalogs, unmonitored, and
leaves a private review file for classification after startup. This avoids
speculative searches and keeps conversion moving, but does not infer a work's
true format. A single monitored
edition of each format is preserved on its corresponding ChaptarrNG record;
multiple monitored editions of the same format still fail closed. The
populated-library rehearsal accounted for every source row; some book
classifications still required human review, and test choices were not
reviewed as correct library decisions. A separate 2026-10-18
Docker image-switch smoke used an empty database created by BookshelfNG `.71`:
ChaptarrNG `.977` converted it on normal startup, retained the original database,
and restored it through rollback; the BookshelfNG image started again on port
8787. This does not validate real user data or the Unraid Community Apps flow.

A follow-up 2026-10-18 image-switch rehearsal used a populated synthetic
database from the BookshelfNG `.71` Hardcover image. The previous Chaptarr
candidate, started without `HARDCOVER=true`, assigned bare numeric provider IDs
to Goodreads. Repeating the switch with the source provider mode preserved
Hardcover IDs, converted data and API results, and passed SQLite integrity and
foreign-key checks. Rollback preview was byte-preserving; rollback restored the
source database byte-for-byte, kept the converted database as a backup, and the
`.71` image reopened the restored source. ChaptarrNG now fails closed when a
bare numeric ID has no explicit provider mode and builds separate Goodreads
and Hardcover compatibility targets for the legacy aliases. Those changes are
source-only. Exact-head run [39175250100](https://github.com/snapetech-org/chaptarrng/actions/runs/39175250100)
passed the provider-mode regression and PostgreSQL fixture checks. The final
Linux artifact refused conversion without a provider mode, preserved Goodreads
and Hardcover identities with their explicit modes, converted during normal
startup, and restored the source byte-for-byte during rollback. Local BuildKit
target checks and hosted alias/bridge regression scripts also pass; the actual
GHCR compatibility images and alias dispatch have not been built or published.
This synthetic rehearsal does not establish operator-data acceptance or
validate the Unraid template handoff; details are in the
[fixture evidence](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md).

BookshelfNG's `BookImportMinimumMatchPercent` setting (default 70, valid range
50–99) affects fuzzy title parsing and import acceptance. The current source
converter retains the original row and, when no explicit ChaptarrNG mode is
present, translates the nearest policy band: 50–66 to Aggressive, 67–82 to
Balanced, and 83–99 to Strict. The migration report names the chosen mode, and
invalid values stop conversion. This is deliberately an approximation because
the two applications use different matching algorithms; validate the selected
mode against expected behavior on converted data. The mapping is not in public
`.977`.

For a source earlier than migration 14, first update BookshelfNG to its final
`0.4.21.71` release so it can bring the database to migration 158. The `.977`
converter does not include the later `main` fix, and no follow-up release is
available while the release freeze is active. The published `.977` converter
does not accept PostgreSQL databases or merge two populated BookshelfNG
databases from one appdata directory. Independently operated instances can be
migrated separately with isolated appdata directories. The separate work-branch PostgreSQL candidate supports only one
migration-158 source schema and must not be used by operators until its
acceptance gates close. The
BookshelfNG helper archives appdata, but
media/download volumes and settings stored outside appdata need separate
handling. Keep the BookshelfNG service stopped, retain an independent backup,
and follow the
[migration guide](MIGRATE_FROM_BOOKSHELFNG.md).

## Legacy install channels

| Channel | Published ChaptarrNG state | BookshelfNG handoff state |
| --- | --- | --- |
| Docker / GHCR | `ghcr.io/snapetech-org/chaptarrng:0.9.977`, `:latest`, and `:main` resolve to the same published multi-architecture image digest. | Legacy aliases remain unchanged. The current ChaptarrNG source candidate builds separate compatibility targets: `latest`, `main`, and `hardcover` preserve native Hardcover mode; `softcover` preserves Goodreads mode. Local BuildKit target checks and hosted alias/bridge regression scripts pass, and the exact Linux binary passed a synthetic provider-mode migration and rollback rehearsal. The GHCR image build, manifests, and alias dispatch were not run or published; the bridge stays disabled. Pinned version tags and digests need an operator change. |
| AUR | `chaptarrng-bin 0.9.977-1` is published. | `bookshelfng-bin 0.4.21.71-1` remains public. A local `.977-1` candidate passed isolated pacman upgrade/downgrade, synthetic populated migration/restore, and active systemd service handoff checks. The test used a network-isolated Arch container and a synthetic migration-158 database; representative operator data and host-specific settings remain open. |
| Fedora COPR | `chaptarrng 0.9.977-1` built successfully for Fedora 43, 44, 45, and Rawhide x86_64. | The BookshelfNG bridge trigger was skipped; current `slskdn/bookshelfng` repo metadata has no packages for Fedora 43, 44, 45, or Rawhide x86_64. A local Fedora 44 RPM transaction upgraded the published `.71` package to a `.977` candidate and downgraded back, preserving config/data canaries. `systemctl` was mocked and RPM dependencies bypassed; live COPR/DNF and real-systemd handoff remain untested. |
| Chocolatey | The `.977` package built and the hosted BookshelfNG-to-ChaptarrNG upgrade smoke passed, but Chocolatey returned HTTP 403 on publish; the live feed has no `chaptarrng` entry. | BookshelfNG `0.4.21.70` remains public. Its identity was not submitted because the bridge is disabled. |
| Unraid | The ChaptarrNG template is maintained separately. | The legacy-template handoff is in [BookshelfNG Unraid PR #5](https://github.com/snapetech-org/bookshelfng-unraid/pull/5); its XML and settings-preservation check passed, but the Community Applications upgrade and rollback flow has not been tested. Existing installs still need a template update. |
| Helm | A same-name compatibility chart is under review in PR #131. It requires explicit migration approval, source provider mode, and an immutable image digest. Local lint and resource-identity checks pass. | The chart keeps the existing release, StatefulSet, Service, selectors, port, `/config` mount, and PVC name. No chart is published, the legacy OCI chart is unchanged, and no installed PVC upgrade or rollback has been tested. Existing installs will need a chart-source change after the gates close. |
| Other historical channels | ChaptarrNG publishes its standard release artifacts. | PPA, Flatpak, AppImage, standalone installers, third-party catalogs, pinned/offline installs, and manually managed services do not all have a tested identity-preserving bridge. |

The compatibility package and image publishers are implemented behind the
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` gate, which is `false`. The
scheduled BookshelfNG bridge workflow confirmed that it found a stable
ChaptarrNG release but skipped publishing because the gate is off. Do not turn
the gate on until the feature and channel acceptance items below are closed.

### Audiobook merge compatibility

ChaptarrNG main contains an unpublished compatibility implementation for
BookshelfNG's opt-in `BOOKSHELF_M4B_MERGE` behavior. It is limited
to tracked audiobook downloads with at least two approved, non-M4B tracks for
the same local book and edition, and it keeps the feature disabled unless that
environment variable is explicitly `true`. The candidate preserves disc/track
tag ordering and track-title chapters, uses the legacy 128 kbps AAC stereo
44.1 kHz defaults (with the existing bitrate override), honors a configured
FFmpeg path when the executable is named `ffmpeg` (`ffmpeg.exe` on Windows),
and falls back to importing the original tracks if merging fails. After a
successful import it removes only the consumed source tracks in move mode and
keeps them in copy mode.

This is source-only and is not in `v0.9.977`. The current retirement candidate
passes a six-case tool-enabled importer smoke with generated two-track input
and the real converter. The run on candidate `12f290bd` passed 6/6 on `kspls0`,
using `m4b-tool` 0.5.2, `mp4v2` 2.1.1, and Debian Bookworm FFmpeg in the
isolated tool image. The successful cases check track ordering, chapter titles
and offsets, album and title metadata, library registration, source deletion
in Move mode, and source retention in Copy mode. Failure cases verify fallback
to separate original-track imports in both modes when a source cannot be probed
and when `m4b-tool` fails after a controlled FFmpeg conversion error. The Linux
build workflow runs the real-tool merge checks in a pinned tool-enabled
container, including synthetic inputs with embedded chapters in Move and Copy
modes. The candidate also retags generated output with TagLibSharp and verifies
chapter titles and offsets survive; those added cases passed in exact-head
Linux Build [39288736177](https://github.com/snapetech-org/chaptarrng/actions/runs/39288736177).
Representative files and restart/resume behavior remain open. It
does not close the Audiobook processing and tags row in
[the feature matrix](FORK_FEATURES.md). Keep the legacy merge setting unset
until a build containing the accepted implementation is installed.

BookshelfNG's in-app updater checks the Readarr-owned update service, which
Snapetech cannot redirect. No single server-side change can move every pinned,
offline, third-party, or manually installed copy automatically.

## Retirement acceptance

BookshelfNG is not ready to retire solely because the converter and a
ChaptarrNG release exist. No new ChaptarrNG release or legacy redirect is
authorized while these gates remain open. The remaining work is:

1. Close each BookshelfNG feature row with behavioral and converted-data
   evidence, or record an explicit maintainer decision to retire that behavior.
   This includes validating the lossy numeric matching-policy translation
   against converted data. The cross-catalog fallback is merged source work,
   not in `.977`; it needs identifier, exact-name, ambiguity, and pending-import
   acceptance evidence.
2. Keep the migration-1-to-13 fix on `main` unreleased until the full gate
   passes; validate older-schema support on representative operator data,
   then validate
   old-identity AUR, COPR, Chocolatey, Helm, container, and Unraid
   upgrades through install, conversion, restart, backup retention, and
   rollback. The local Fedora RPM transaction rehearsal proves package/file
   handling only; validate a live COPR/DNF handoff if that identity is kept.
3. Resolve the Chocolatey HTTP 403; the `.977` COPR build is verified. The
   old-identity RPM package transaction was rehearsed locally, but the legacy
   COPR repo currently has no packages and no live DNF handoff was tested;
   verify each published package checksum or image digest against the tested
   artifact.
4. Decide and document handoffs for historical channels that cannot be
   redirected. Preserve a manual migration path for pinned and offline installs.
5. Run the final migration against representative operator data, review every
   ambiguous media choice, and verify external clients against the converted
   library before enabling automatic aliases.

The conversion preview, source-side archive helper, synthetic conversion checks, and package templates are present. A populated migration-158 SQLite copy has now completed apply and rollback with the public image, but an in-place deployment cutover, full feature parity, all legacy package publications, and a universal automatic update path are not established. See the
[BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
and the linked parity roadmap for the detailed evidence ledger.
