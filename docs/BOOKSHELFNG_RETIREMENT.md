# BookshelfNG retirement status

**Reviewed:** 2026-10-19 after BookshelfNG `main` added the SeerrNG edition
catalog endpoint and the ChaptarrNG candidate passed a populated SQLite
preview/apply/restore rehearsal. ChaptarrNG `v0.9.977` remains the latest
published release; PR #128 merged the fail-closed release and distribution
gates. This page is the current
handoff summary for moving BookshelfNG users to ChaptarrNG. The
detailed feature inventory remains in the
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md).

## Current releases

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
  passed on its clean head. The current retirement candidate passes a
  tool-enabled synthetic two-track importer smoke using the real converter; it
  verifies track order, chapter titles and offsets, metadata, one registered
  file, Move-mode source cleanup, and Copy-mode source retention. Focused
  importer tests also cover merge selection and fallback with a mocked
  converter. The six-case real-tool smoke also verifies fallback to separate
  original-track imports in Move and Copy modes when one source cannot be
  probed, and when `m4b-tool` fails after a controlled FFmpeg conversion error.
  Representative media, embedded-chapter compatibility, and restart or resume
  behavior remain open. This source is not in `.977`.
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
  The earlier synthetic provider-mode, startup, and rollback evidence is in the
  [detailed fixture report](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md).
  This is not operator-data or channel acceptance.
- On 2026-10-19, the unpublished PR #131 candidate (build version
  `0.9.978.0`) passed preview, explicit apply, rollback preview, and explicit
  rollback on a disposable copy of populated migration-158 SQLite data with
  `HARDCOVER=true` and network access disabled. Migration preview left the
  source unchanged; apply produced schema 121 and retained the source. Restore
  preview left the converted database and source backup unchanged; rollback
  restored a byte-identical schema-158 backup and preserved the converted
  database separately. Integrity and foreign-key checks passed, and the
  original private snapshot remained unchanged. The importer fixes prompted
  by this rehearsal preserve missing-edition files as unmapped and retain
  conflicting history on its source book without attaching an unsafe file or
  edition. Focused importer, edition-catalog, and capability tests pass 52/52
  locally. Exact-head run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646)
  passed on PR #131 head 2c0401fc: Linux, macOS, and Windows build/tests,
  PostgreSQL migrations, release-note validation, CodeQL, dependency and
  secret checks, NuGet submission, and runner-receipt reconciliation. The
  preceding run [39195460843](https://github.com/snapetech-org/chaptarrng/actions/runs/39195460843)
  exposed four Windows fixtures using POSIX-only paths; the follow-up uses
  native temporary paths and passes. Disposable-fork validation was skipped
  by its gate. This validates hosted candidate code, not operator cutover.
  Ambiguous format review, PostgreSQL operator data, other source shapes,
  complete feature parity, installed rollback, and channel acceptance remain
  open.
- BookshelfNG `main` at `5e7cda41` now exposes
  `GET /api/v1/book/{id}/editions/catalog` and advertises `editionCatalog` for
  SeerrNG edition cards. The ChaptarrNG candidate adds the matching route and
  capability, plus an **Editions and Narrations** tab on book details for
  reading known edition metadata, including duration. Unstated details remain
  visibly unprovided.
  Candidate code commit `6a6b7d1f` integrates current ChaptarrNG `main`,
  including its Editions tab and series narrator comparison, and retains the
  edition labels for unknown and not-applicable metadata. Local translation,
  type, targeted ESLint, and Stylelint checks pass; exact-head hosted
  validation is pending. The route and catalog implementation at code
  snapshot `2c0401fc` passed run
  [39196795646](https://github.com/snapetech-org/chaptarrng/actions/runs/39196795646).
  The current source candidate also passed the isolated SeerrNG Compose smoke
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
  passes on head 2c0401fc, including Windows tests after replacing the
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
| Other historical channels | ChaptarrNG publishes its standard release artifacts. | PPA, Flatpak, AppImage, Helm, standalone installers, third-party catalogs, pinned/offline installs, and manually managed services do not all have a tested identity-preserving bridge. |

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
and the real converter. The successful cases check track ordering, chapter
titles and offsets, album and title metadata, library registration, source
deletion in Move mode, and source retention in Copy mode. Failure cases verify
fallback to separate original-track imports in both modes when a source cannot
be probed and when `m4b-tool` fails after a controlled FFmpeg conversion error.
Representative files, embedded-chapter compatibility, and restart/resume
behavior remain open. It does not close the Audiobook processing and tags row in
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
