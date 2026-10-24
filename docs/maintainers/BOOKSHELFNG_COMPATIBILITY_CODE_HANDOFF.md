# BookshelfNG compatibility code handoff

This is the third code checkpoint after the 2026-10-24 replacement
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
  propagate failed service starts.

The Linux default-unit cutover now has automatic database and application
rollback code. C3/F4 still needs Luna's package-manager failure-path and
restart acceptance before it can be treated as closed.

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
   backups.

## Still open after this checkpoint

- Linux package handoff has automatic rollback code for supported default
  service units. It remains unaccepted until Luna exercises every package
  manager's service failure and restart behavior, especially when systemd
  executes `ExecStopPost` after a failed `ExecStartPost`.
- BookshelfNG-to-ChaptarrNG acquisition, import, settings, integration, and UI
  parity reviews from C4-C6 remain open.
- Channel delivery and installed-upgrade acceptance from C7-C8 remain open.
- These code slices have not been built or tested. Luna's results should be
  tied to the exact commit that contains them; regressions should be fixed
  before the next code batch.
