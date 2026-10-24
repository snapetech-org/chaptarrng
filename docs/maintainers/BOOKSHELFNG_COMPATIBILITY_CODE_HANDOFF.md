# BookshelfNG compatibility code handoff

This is the first code checkpoint after the 2026-10-24 replacement reassessment.
It is intended for focused validation and refinement before broad parity work.

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

## Luna validation queue

1. Verify the importer profile in SQLite and PostgreSQL conversions for
   `HARDCOVER` unset, `true`, and `true` with `HARDCOVER_NATIVE=false`. Include
   source databases with bare IDs and already-prefixed IDs. Confirm the source
   backup stays unchanged and duplicate profile keys fail before promotion.
2. On a migrated database, call unchanged `/api/v1` author/book lookup, add,
   list, update, and delete routes with bare Goodreads and Hardcover IDs. Check
   both the provider used internally and the provider ID projected back to the
   client.
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

## Still open after this checkpoint

- Linux package handoff remains without a shared durable journal and automatic
  application/data/service rollback. The database converters still retain
  source backups, but that is not a package-level rollback transaction.
- BookshelfNG-to-ChaptarrNG acquisition, import, settings, integration, and UI
  parity reviews from C4-C6 remain open.
- Channel delivery and installed-upgrade acceptance from C7-C8 remain open.
- This checkpoint has not been built or tested. Luna's results should be tied to
  the exact commit that contains it; regressions should be fixed before the
  next code batch.
