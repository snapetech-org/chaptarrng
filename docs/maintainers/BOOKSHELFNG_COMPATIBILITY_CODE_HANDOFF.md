# BookshelfNG compatibility code handoff

This is the tenth code checkpoint after the 2026-10-24 replacement
reassessment. It is intended for focused validation and refinement before broad
parity work.

## Implemented in this checkpoint

- The BookshelfNG importer now records a compatibility-profile version and the
  source provider dialect (`hc` or `gr`) in the converted database.
- Bare numeric source IDs now follow BookshelfNG's actual runtime predicate:
  native Hardcover is selected only when `HARDCOVER=true` and
  `HARDCOVER_NATIVE` is not `false`; an unset `HARDCOVER` therefore means
  Goodreads. The importer persists the resulting dialect and target mode.
- On a migrated installation, requests to the original `/api/v1` address use
  the recorded source dialect unless the caller identifies itself as a native
  ChaptarrNG client. Existing `/readarr/...` and media-scoped routes keep their
  explicit path behavior.
- The ChaptarrNG frontend marks its API requests with
  `X-ChaptarrNG-Client: native` (or `chaptarrngClient=native` for its EventSource
  request), keeping canonical IDs and native validation available after
  migration.
- An unscoped legacy book add uses the one monitored edition's format, matching
  BookshelfNG's single-work add model. It no longer creates both format rows for
  that legacy request. Legacy author and book adds do not enter ChaptarrNG's
  pending-import protocol.
- The unchanged `/api/v1` address now projects split audiobook/ebook rows back
  to one BookshelfNG book ID. The monitored source edition selects the legacy
  detail row, paged reads retain the old title order and one-work total, edits
  resolve the submitted edition to its target row, monitoring applies to the
  legacy work, old pagination values clamp to BookshelfNG's 1–200 page-size and
  non-negative offset behavior, and edition/file responses translate retained
  source IDs.
- Queue and history filters continue to resolve a legacy book ID to all mapped
  format rows, while queue/history response IDs and included book resources
  project those rows back to the original BookshelfNG work ID. Book responses
  also restore the original `AuthorMetadataId` after metadata rows are merged
  into ChaptarrNG author records. Rename previews include every mapped format
  and project retained book, edition, and file IDs back to the source handles.
- Legacy local book IDs now resolve only when the persisted BookshelfNG
  compatibility context is active. Native clients keep using canonical target
  IDs even when one numeric value overlaps a migrated source ID. The same
  context-aware resolution covers book editors, book files, editions, queue and
  history filters, release search, cover paths, rename/retag/convert previews,
  manual imports, and commands.
- Manual import now resolves source edition IDs before validating their book,
  uses the selected file's format to choose the correct split book row when no
  media type is supplied, and projects nested book, edition, and author
  metadata IDs back to BookshelfNG IDs in preview and update responses.
- Command start translates old book IDs only for legacy requests. Command list
  and detail responses project book IDs in supported command bodies, and
  mapped command/book/file SignalR changes emit `Sync` so each client reloads
  through its own API dialect instead of receiving a target ID. Release results
  and bulk book-editor responses also project split rows back to the old work
  ID.
- A legacy `RefreshBook` command that resolves to both format rows now keeps
  the original command type and response shape while refreshing both rows. Its
  internal fan-out IDs are removed from compatibility responses after the
  original BookshelfNG work ID is projected back.
- Migrated installs now use the BookshelfNG release comparer for same-format
  audiobook and ebook decisions. Mixed-format decisions keep ChaptarrNG's
  format preference; native installs keep the current comparer.
- Linux Debian/PPA, RPM/COPR, and AUR package upgrades now record a durable
  migration-pending marker and a root-only copy of the prior application and
  service unit before replacing an installed BookshelfNG package. The service
  runs a database readiness check after startup and clears the marker only
  after the migrated ChaptarrNG database and provider dialect are readable.
  When readiness fails, the service helper selects the newest retained SQLite
  backup or PostgreSQL backup schema, restores BookshelfNG main data, and
  restores PostgreSQL log/cache schemas from verified pre-cutover dumps. It
  then restores the previous app and unit and lets systemd restart that unit.
  It keeps the displaced ChaptarrNG main schema/database and independent
  PostgreSQL dumps for recovery. Custom
  units or drop-ins restore the previous app and default unit and leave
  migration pending with `operator-review-required`; installs without a valid
  pre-upgrade snapshot block automatic conversion. Debian, RPM, and AUR hooks
  propagate failed service starts. A retained private journal, writable only by
  root and the service account, records the
  detected, snapshotted, preflighted, converted, promoted, healthy, rolled-back,
  and intervention-required states, step outcomes, and SHA-256 fingerprints for
  the source app/database/configuration, service unit, and installed artifact.
  Startup reconciliation observes the current database readiness result after a
  restart; journal fingerprints and outcomes remain after healthy cleanup removes
  the temporary application snapshot.
- SQLite and PostgreSQL converters now record source detection, preflight,
  snapshot, validated conversion, and promotion boundaries in that journal.
  They remove abandoned disposable stage artifacts on a later safe conversion
  attempt; PostgreSQL temporary schemas carry a source-schema marker so cleanup
  removes only converter-owned artifacts for the locked source schema. Retained
  rollback copies remain available. Restore and successful database-readability
  checks advance the same journal.
- When a Windows Chocolatey upgrade starts ChaptarrNG during installation, the
  installer now checks the migrated database lineage and persisted source
  provider dialect after `/ping` before it removes the prior app snapshot. The
  converter milestones use the same data-directory journal on Windows.
- When the BookshelfNG service is stopped at upgrade time, Chocolatey now
  retains the prior app and registers a protected SYSTEM recovery task for the
  next service start. The task verifies the configured local health URL plus
  the migrated database/provider profile, then restores source SQLite or
  PostgreSQL data, the previous app, both NSSM environment blocks, arguments,
  and start mode after failure. It preserves the originally stopped state and
  writes channel outcome/state into the shared migration journal.
- AppImage and Flatpak now launch through a shared supervisor that preserves
  the legacy data path, supports an explicit `-data` override, and checks local
  HTTP plus database health. It records `starting`, `healthy`, `rolled-back`, or
  `intervention-required`, writes channel outcome/state into the shared
  migration journal, and restores source data after a failed/interrupted first
  launch. Reopening the old artifact remains the executable rollback.
- A source-pinned C4 pass added migrated-profile branches for BookshelfNG's
  raw-title release terms, quality-first upgrade/cutoff rules, shared author
  tags in delay/protocol/indexer/pending-queue/download-client selection,
  legacy author/book monitoring, and permanent torrent-seeder rejection. The
  remaining source decision specifications were compared; their behavior is
  unchanged, with differences limited to logging. Duplicate-grab suppression
  now resolves split target rows back to one BookshelfNG work ID, so an
  audiobook and ebook row from one legacy work cannot both be grabbed in one
  RSS pass. Source/target blob IDs and dispositions are recorded below.
  Matching/import/filesystem, provider and UI workflows still need their own
  semantic review.

The Linux default-unit cutover now has automatic database and application
rollback code. C3/F4 still needs Luna's package-manager failure-path and
restart acceptance before it can be treated as closed.

## Source comparison completed in this code slice

Source is pinned to BookshelfNG `d42ec2aba1e411f4fb538d4d7201f6930898a91e`.
The target blob IDs below are the exact file contents in this checkpoint.
`implemented-equivalent` means a migrated-profile code path now follows the
observed source contract; Luna still needs to verify the behavior on fixtures
and installed data.

| Acceptance ID | Contract reviewed | Source path (source blob) | Target path (target blob) | Disposition |
| --- | --- | --- | --- | --- |
| C4-01 | Match release-profile required/ignored terms against the original title and shared author tags; keep rejection permanent. | `ReleaseRestrictionsSpecification.cs` (`a2585901140e1d05f8726b6cc269d181f6823533`) | `ReleaseRestrictionsSpecification.cs` (`7c2a871cc7b1d43648ea70e26915db7d76ef9796`) | `implemented-equivalent` |
| C4-02 | Compare quality before custom-format score and preserve source revision behavior. | `UpgradableSpecification.cs` (`7d536e93cf159131dd9e9e779d558f07aecbca5b`) | `UpgradableSpecification.cs` (`620601565dba5ffb28c8514536c94df50fdb4f15`) | `implemented-equivalent` |
| C4-03 | Use source quality/cutoff behavior without planned-conversion or target split-part filters. | `CutoffSpecification.cs` (`40db4dd2d384ecc6f4a4d2d35bbf7175061a0b12`) | `CutoffSpecification.cs` (`eeafc455aecb34973cb40466e6d0d4fcedd40780`) | `implemented-equivalent` |
| C4-04 | Evaluate queued candidates against source quality rather than planned target conversion. | `QueueSpecification.cs` (`58a6535d46d0b5c63ce197ea0b13c7397bc47a37`) | `QueueSpecification.cs` (`1ef8a0eaf6af0d708e07eaa0487ea3869b102074`) | `implemented-equivalent` |
| C4-05 | Compare RSS history with source qualities and source upgrade rules. | `HistorySpecification.cs` (`9074091c95decf0f474657e647761081e515fa40`) | `HistorySpecification.cs` (`ccbbd2c367e2e9c1adb9e25978033a3399ea6c5d`) | `implemented-equivalent` |
| C4-06 | Keep upgrade-allowed decisions quality-first and compare only compatible file qualities. | `UpgradeAllowedSpecification.cs` (`7692f4363aabaf951108d14d141dca470fd99292`) | `UpgradeAllowedSpecification.cs` (`c8e2fc263e175809f1fc872febbc220ca242c755`) | `implemented-equivalent` |
| C4-07 | Reject an equal-or-better compatible on-disk file using the source preference order. | `UpgradeDiskSpecification.cs` (`d5214d1747816996a99e6cd9aa17ca219973ecae`) | `UpgradeDiskSpecification.cs` (`7f221510f182225259d3621bfee255238b382097`) | `implemented-equivalent` |
| C4-08 | Select delay protocol from the shared author tag set. | `ProtocolSpecification.cs` (`cd9c6a04b5e3083e6c2dac8d24749183eec3bc9e`) | `ProtocolSpecification.cs` (`6765fff4402d77ab3b40729cdb439b96f385ec0f`) | `implemented-equivalent` |
| C4-09 | Select RSS delay rules from shared author tags. | `DelaySpecification.cs` (`a6600ba229e43e9710d3f699c73ee8f301a53a28`) | `DelaySpecification.cs` (`bce8c92f1eff9e3ce5c9a8c6ef5ea0148d43199e`) | `implemented-equivalent` |
| C4-10 | Match indexer tags against the shared author tags. | `IndexerTagSpecification.cs` (`07c01f2cdcbe077fa876f1351ac419ab3f046e8a`) | `IndexerTagSpecification.cs` (`2e3f413891b71f0e095ce652c3531a49b7c381d8`) | `implemented-equivalent` |
| C4-11 | Gate RSS using the legacy author and source-work monitoring fields. | `MonitoredBookSpecification.cs` (`f88178d5b0eacfd569c49bbf6597e30e5612b618`) | `MonitoredBookSpecification.cs` (`ea54afe574b0fb3f7fcd83b4b88dea93c0a37656`) | `implemented-equivalent` |
| C4-12 | Keep minimum-seeder rejection permanent for migrated installs. | `TorrentSeedingSpecification.cs` (`53313042ebada5fc44deac3bccf4590fe6bffeb5`) | `TorrentSeedingSpecification.cs` (`3915c90290083bf74ff321f29b5a9955d74f77fa`) | `implemented-equivalent` |
| C4-13 | Choose pending candidates by source quality/revision first and shared author-tag protocol preference for both formats. | `PendingReleaseService.cs` (`3f2c0973d7ad2d4698cc89730300a8e54ec13d77`) | `PendingReleaseService.cs` (`99f2f47e1328efbee536554f218f6e969b1decc0`) | `implemented-equivalent` |
| C4-14 | Select a download client from the shared BookshelfNG author tags for both media types. | `src/NzbDrone.Core/Download/DownloadService.cs` (`a61b2393ab7730d6307a8d099bcd6b534260f6f6`) | `src/NzbDrone.Core/Download/DownloadService.cs` (`0bd75d9ae29c8747984e0019255fad7d1f8f6218`) | `implemented-equivalent` |

The remaining release-decision specifications were source-compared at the
pinned revision. Each target change below is a logging-level or safe-log
format change; the accepted/rejected decision and reason are unchanged.

| Acceptance ID | Source path (blob) | Target path (blob) | Disposition |
| --- | --- | --- | --- |
| C4-15 | `src/NzbDrone.Core/DecisionEngine/Specifications/AcceptableSizeSpecification.cs` (`5cc9e762ccaa73db305cc9e9d67ac218e306739f`) | same path (`9af8920165c3952835b4f4bf7c2999ab15c97dec`) | `validation-only` |
| C4-16 | `src/NzbDrone.Core/DecisionEngine/Specifications/AlreadyImportedSpecification.cs` (`8dfa646975af5fa862346096c7127bd5e8ce3ad4`) | same path (`0b171be3bb2acfb9be6fd90d32589739a686484e`) | `validation-only` |
| C4-17 | `src/NzbDrone.Core/DecisionEngine/Specifications/BlocklistSpecification.cs` (`8e6349a365d9e5a6bdc749a37330fc3493415104`) | same path (`1a3d6086462f97182cec90257f2930f94a941475`) | `validation-only` |
| C4-18 | `src/NzbDrone.Core/DecisionEngine/Specifications/EarlyReleaseSpecification.cs` (`13984bc28582624700770812a485d38c367a94e6`) | same path (`390c17c4e562b354313da77cf9608608f7fa5a89`) | `validation-only` |
| C4-19 | `src/NzbDrone.Core/DecisionEngine/Specifications/MaximumSizeSpecification.cs` (`6562f1ef8b3685b52bbbb8ab447194f790c02c61`) | same path (`df2c9097e29ef65a7291c05875a185fbdc7e81d0`) | `validation-only` |
| C4-20 | `src/NzbDrone.Core/DecisionEngine/Specifications/MinimumAgeSpecification.cs` (`ec6a050ebbcbf0afdfc4d3ea863e0c6ea25fd80b`) | same path (`e599c8dae571a3feb4cdfc76bd65f249b24e715c`) | `validation-only` |
| C4-21 | `src/NzbDrone.Core/DecisionEngine/Specifications/NotSampleSpecification.cs` (`7695d2c1255264b11a93c7fa7834a618f6e237b9`) | same path (`36df13f4a4eaabbf030bb984d4a66822e4ee6084`) | `validation-only` |
| C4-22 | `src/NzbDrone.Core/DecisionEngine/Specifications/RawDiskSpecification.cs` (`cd6855e51ad5d7752589e9ffc2bf38f0ce09ac9e`) | same path (`0f34e598d6ec91ec3aaaca00a519eeabf9ac91cf`) | `validation-only` |
| C4-23 | `src/NzbDrone.Core/DecisionEngine/Specifications/RepackSpecification.cs` (`1cf9ffa4dce91a31a0c5239ebd141a8f39df15a5`) | same path (`f564dab8582f16693dd7b382b39da0babef3605b`) | `validation-only` |
| C4-24 | `src/NzbDrone.Core/DecisionEngine/Specifications/RetentionSpecification.cs` (`94c7f520079a0910705b437564ea117c65bc523e`) | same path (`005f089cae34178aa0c062d20fcde4db22c063e3`) | `validation-only` |
| C4-25 | `src/NzbDrone.Core/DecisionEngine/Specifications/Search/AuthorSpecification.cs` (`94aa06322855badf008bdc43ec2ebd16b846cdd4`) | same path (`421031c1d6ca1171194fb8dbb9a686234c5eaaf2`) | `validation-only` |
| C4-26 | `src/NzbDrone.Core/DecisionEngine/Specifications/Search/BookRequestedSpecification.cs` (`0351c8e6a76a7b6c03f29c0e9a333270a72e8045`) | same path (`65bcd58c3282f81039e22f3d7d4e11c0e39ff8e9`) | `validation-only` |
| C4-27 | `src/NzbDrone.Core/DecisionEngine/Specifications/Search/SingleBookSearchMatchSpecification.cs` (`849684dba347aa12b6a17179f27bc264445b985d`) | same path (`c0f9814fd1df65ce4fee6bc490d1a3b812b1fb1c`) | `validation-only` |
| C4-28 | `src/NzbDrone.Core/DecisionEngine/Specifications/RssSync/DeletedBookFileSpecification.cs` (`f0ac87d85e6df3d136e4a7115c2d5c7f9b6068d0`) | same path (`757f569bab21a8603209ef899d20e748de8863bb`) | `validation-only` |
| C4-29 | `src/NzbDrone.Core/DecisionEngine/Specifications/RssSync/ProperSpecification.cs` (`ae3809bf9ca6a695d53a30e4c5dee403fd914706`) | same path (`be5b18053215c1f2d44b618e82726e5be54c4c40`) | `validation-only` |
| C4-30 | Suppress a second grab for the same source work even when migration split it into audiobook and ebook target IDs. | `src/NzbDrone.Core/Download/ProcessDownloadDecisions.cs` (`ebba934c1cc3e87b14b4c8fb1287dad80e355e11`) | same path (`7070f324555efc828be020c279cf103cb7a1c7b5`) | `implemented-equivalent` |

## Additional C5 and C6 review slices

| Acceptance ID | Contract reviewed | Source path (source blob) | Target path (target blob) | Disposition |
| --- | --- | --- | --- | --- |
| C5-01 | Preserve known provider implementations, settings contracts, and credentials; map the renamed Readarr import-list provider; block any unknown implementation or meaningful setting before apply. | `src/NzbDrone.Core/ImportLists/Readarr/ReadarrImport.cs` (`7e9a83545d0751276fbe39cbe2922458dfa15bd5`) | `BookshelfDatabaseImporter.cs` (`814a5ba92795834a7006ff80ac38c31969c56843`), `ChaptarrImport.cs` (`93c069f4ec543395f0de59b43f54c5aef01be442`) | `implemented-equivalent` for recognized contracts; unsupported values fail closed |
| C6-01 | Keep native ChaptarrNG API calls on canonical IDs after migration, including the global AJAX client, direct encrypted-backup fetch, bootstrap localization fetch, and native SSE query. Login localization and `/initialize.json` carry no provider IDs and do not require an identity dialect. | `frontend/src/Utilities/createAjaxRequest.js` (`0f025d74c3f4f680f34df3aa65a71525f9def5fc`) | same path (`32896f9d098402458300a7463cac574ce313acaa`); direct-call paths reviewed: `frontend/src/System/Backup/downloadEncryptedBackup.js`, `frontend/src/index.ts`, `frontend/src/Services/SeriesEnrichmentService.js` | `implemented-equivalent` for native request context; broad UI workflow review remains open |

C5-01 was checked against the pinned source provider inventory: all source
download-client base providers have corresponding target providers; the
Readarr self-import list has an explicit `ReadarrImport`/`ReadarrSettings` to
`ChaptarrImport`/`ChaptarrSettings` alias; source notification implementations
are represented in the target; and source Goodreads OAuth fields plus the
Hardcover API token have explicit migration handling. The importer disables
and blocks unknown provider contracts or meaningful settings instead of
silently dropping them. This does not close auth, webhook, or external-service
runtime acceptance.

## Luna validation queue

1. Verify the importer profile in SQLite and PostgreSQL conversions for
   `HARDCOVER` unset, `true`, and `true` with `HARDCOVER_NATIVE=false`. Include
   source databases with bare IDs and already-prefixed IDs. Confirm the source
   backup stays unchanged and duplicate profile keys fail before promotion.
2. On a migrated database, call unchanged `/api/v1` author/book lookup, add,
   list, detail-by-ID, paged list, update, monitor, delete, edition, and book
   file routes with bare Goodreads and Hardcover IDs. Seed a source work that
   split into ebook and audiobook rows; verify it appears once, keeps the old
   ID, resolves the monitored edition on detail/update, combines file counts,
   and does not edit or delete an unrelated work. Check the provider used
   internally and the provider ID projected back to the client. Verify queue
   details, paged queue, history by book, history since, and author history use
   the old book ID in both `BookId` and included `Book` resources, and preserve
   the source `AuthorMetadataId` in included and direct book responses. A rename
   preview for a split work should include both mapped formats with the legacy
   book/file/edition IDs. Confirm `/books/paged` keeps source title ordering,
   one-work totals, and BookshelfNG's pagination clamping for negative offsets,
   zero page size, and sizes over 200.
3. Repeat native frontend/API calls with `X-ChaptarrNG-Client: native`; confirm
   prefixed IDs stay canonical and bare IDs receive native validation errors.
   Confirm the old `/readarr/{hc|gr}/{ebook|audiobook}` and `/ebook` routes still
   scope media type and provider dialect.
4. Post a BookshelfNG-shaped book with exactly one monitored ebook edition and
   one with exactly one monitored audiobook edition. Verify only the selected
   format is added, its monitored edition remains selected, the returned
   resource is legacy-projectable, and no pending-import 202 is emitted. Cover
   missing/multiple selected editions and unavailable author metadata.
5. Verify an unavailable author add returns the legacy error path without
   creating a pending-import row, while native requests retain their existing
   queue behavior.
6. On Debian/PPA, RPM/COPR, and AUR, upgrade active and inactive installed
   BookshelfNG services. Force startup readiness to fail after SQLite and
   PostgreSQL promotion; confirm the newest retained source backup is restored,
   the displaced ChaptarrNG database is retained, the old executable and unit
   return, and systemd restarts BookshelfNG. For PostgreSQL, verify the
   pre-cutover dump covers main/log/cache, rollback restores BookshelfNG
   log/cache state, and the displaced ChaptarrNG log/cache state remains in its
   own verified dumps beside the retained ChaptarrNG main schema. Cover failure
   before database promotion, a missing or invalid backup, and custom
   unit/drop-in setups. Confirm successful readiness clears the marker and
   removes only the old application snapshot, while retaining database
   backups. Inspect the private journal for the expected state sequence,
   outcome records, and fingerprints; confirm a restart after promotion
   advances from observed database lineage without replacing original source
   fingerprints.
7. Start, list, fetch, and cancel commands through the unchanged API address.
   Confirm supported command bodies send and return BookshelfNG IDs, including
   multi-format work IDs. For a `RefreshBook` command targeting a work split
   across both formats, confirm both rows refresh while the command name remains
   `RefreshBook` and its legacy body retains the scalar work ID. Confirm command
   bodies are not mutated in the queue
   when a legacy response is projected. Repeat from a native client where the
   same numeric value is both a source ID and a Chaptarr target ID; the native
   request must keep the target row. Verify mapped book and file changes
   publish `Sync`, then confirm old and native clients reload their own
   projected resources.
8. Run manual-import preview and update with a split work, one ebook path and
   one audiobook path, and the same old Book ID. Confirm the selected target
   row follows the file extension, a submitted legacy Edition ID resolves to
   its mapped edition, mismatched editions are rejected, and nested response
   Book/Edition/AuthorMetadata IDs remain in the BookshelfNG dialect.
9. Fetch release results and save/delete through the bulk book editor on both
   migrated and native API contexts. Confirm release `BookId`, editor response
   IDs, monitoring, and deletion stay scoped to the expected legacy work or
   native target row. For migrated installs, compare audiobook and ebook release
   ordering against BookshelfNG for quality, revision preference, custom format
   score, protocol, indexer priority, torrent health, release scope, age, and
   size; confirm mixed-format and native ordering remain unchanged.
10. Upgrade an active Windows Chocolatey BookshelfNG service. After `/ping`
    succeeds, confirm `/check-bookshelf-migration` validates the target database
    and provider dialect before the installer discards the prior application.
    Force that check to fail and confirm the installer restores the database,
    previous application, service settings, environment, and prior running state.
11. Upgrade a **stopped** Windows Chocolatey service with custom `-data`,
    `-port`, URL base, NSSM `AppEnvironment`, and `AppEnvironmentExtra`. Confirm
    the recovery task and protected manifest are registered before cutover.
    Start the service and verify success removes only the prior app snapshot.
    Force a failed start after SQLite and PostgreSQL promotion; confirm recovery
    restores the source database, prior app, both NSSM environment blocks,
    arguments and start mode, and leaves the service stopped as it was before
    upgrade. Confirm task cleanup and a subsequent Chocolatey retry.
12. From the same AppImage path and Flatpak app ID, verify success and failed
    startup for SQLite and PostgreSQL, plus process interruption after database
    promotion. Preserve default and explicit `-data` paths, config/environment/
    CLI port and URL-base overrides, source backups, and readable recovery
    state. Reopen the old artifact after rollback and confirm it reads the
    restored source database. Test single-instance locking and retry only after
    a recorded rollback.
13. On migrated fixtures, compare release-profile raw-title terms, quality vs
    custom-format ranking, proper/revision behavior, cutoff, compatible formats,
    RSS/history, delay/protocol/indexer tags, pending queue winner, monitored
    author/work filtering, and permanent minimum-seeder rejection against the
    pinned BookshelfNG source. Confirm all branches are inactive for native
    ChaptarrNG installs.
14. Give one source BookshelfNG work both an audiobook and ebook target mapping.
    Submit equivalent approved decisions for both rows in one RSS pass: the
    migrated profile must grab at most one work and classify any delayed second
    candidate as a fallback. Repeat with compatibility disabled and confirm
    native media rows stay independent. Include an unmapped native target ID
    numerically equal to a legacy source ID to confirm identity namespaces do
    not collide.
15. Re-run the provider conversion fixtures for built-in indexers,
    download-clients, import-lists and notifications; verify the renamed
    Readarr self-import alias, full and partial Goodreads OAuth, Hardcover
    library token, nested unknown settings, and report redaction. Unknown
    meaningful settings must block promotion before source data is changed.
16. On a migrated profile, exercise the source-facing Book/Author search and
    add flows, detail and edit pages, mixed-format monitoring and bulk edits,
    manual import, unmapped-file retry, queues, backup/restore, and Settings
    forms. Verify native API calls retain canonical target IDs, source calls
    retain BookshelfNG IDs, and every direct UI API/SSE request uses the right
    native request marker or intentionally has no provider-ID context.

## Still open after this checkpoint

- Linux package handoff has automatic rollback code for supported default
  service units. It remains unaccepted until Luna exercises every package
  manager's service failure and restart behavior, especially when systemd
  executes `ExecStopPost` after a failed `ExecStartPost`.
- The reviewed C4 release-decision and duplicate-grab contracts are implemented
  but unverified. Remaining C4 download completion, matching/import/filesystem,
  catalog refresh and media workflows remain open. C5 provider contract import
  is source-reviewed, while broader auth, webhook, external integration, and
  runtime behavior remain open. C6 native request identity is source-reviewed;
  the user-facing search, edit, settings, mixed-format, and recovery workflows
  still need their own semantic review.
- Channel delivery and installed-upgrade acceptance from C7-C8 remain open.
- Windows Chocolatey deferred-start and desktop first-launch recovery now
  coordinate channel outcomes with the shared migration journal. They have no
  Luna installed-channel acceptance yet. Desktop executable rollback still
  requires reinstalling/reopening the old artifact.
- These code slices have not been built or tested. Luna's results should be
  tied to the exact commit that contains them; regressions should be fixed
  before the next code batch.
