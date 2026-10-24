# BookshelfNG compatibility code handoff

This is the ninth code checkpoint after the 2026-10-24 replacement
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

The Linux default-unit cutover now has automatic database and application
rollback code. C3/F4 still needs Luna's package-manager failure-path and
restart acceptance before it can be treated as closed.

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

## Still open after this checkpoint

- Linux package handoff has automatic rollback code for supported default
  service units. It remains unaccepted until Luna exercises every package
  manager's service failure and restart behavior, especially when systemd
  executes `ExecStopPost` after a failed `ExecStartPost`.
- BookshelfNG-to-ChaptarrNG acquisition, import, settings, integration, and UI
  parity reviews from C4-C6 remain open.
- Channel delivery and installed-upgrade acceptance from C7-C8 remain open.
- Windows Chocolatey converter checkpoints are journaled, but its package-level
  prior-app/service handoff and deferred-start recovery are not yet coordinated
  through that journal. Desktop handoffs also remain outside the shared journal.
- These code slices have not been built or tested. Luna's results should be
  tied to the exact commit that contains them; regressions should be fixed
  before the next code batch.
