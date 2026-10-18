# BookshelfNG retirement status

**Reviewed:** 2026-10-18 after ChaptarrNG `v0.9.977` was published and PR #128
merged the fail-closed release and distribution gates. This page is the current
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
  passed on its clean head. A synthetic two-track smoke validated the external
  converter and chapter sidecar, but not the importer end to end or
  representative media. This source is not in `.977`.
- BookshelfNG `0.4.21.71` is published as the final source release.
- The BookshelfNG `.71` tag contains issue #114's optional new-author path
  setting. ChaptarrNG has equivalent new-author path initialization in
  `v0.9.974` and later. ChaptarrNG applies configured format defaults
  automatically; it has no separate toggle. Explicit author roots are kept,
  ambiguous or missing defaults stay blank, and existing authors or files are
  not changed.

## Database migration contract

The published ChaptarrNG `v0.9.977` converter accepts the BookshelfNG
migration-158 SQLite database and bundles the BookshelfNG SQLite migration
chain. It upgrades a disposable copy before import and retains the untouched
snapshot as the rollback source. The `.977` converter still rejects versions
1–13 at its pre-upgrade table check; those versions retain `Blacklist` until
the embedded chain renames it. PR #126 merged the version-aware guard and
synthetic migration-1 rollback coverage into `main`, and its hosted migration
checks passed on Linux, macOS, Windows, and PostgreSQL. Neither the source fix
nor synthetic fixtures establish behavior on real older-schema operator data
or every historical release. Schema 159 is rejected without changing the
source. PostgreSQL source databases and two populated BookshelfNG databases
remain unsupported.
ChaptarrNG stages a native database, accounts for source rows, checks SQLite
integrity and foreign keys, and promotes only after validation. Preview does
not modify the source. Apply and rollback are explicit operations, and normal
startup can run the same converter when it finds a supported source database.

Some BookshelfNG books do not contain enough data to infer ebook versus
audiobook. Preview writes a private review file. Operators can provide a
complete, source-matched media map before conversion; with no map, conversion
keeps each unresolved work in both ChaptarrNG catalogs, unmonitored, and
leaves a private review file for classification after startup. This avoids
speculative searches and keeps conversion moving, but does not infer a work's
true format. A single monitored
edition of each format is preserved on its corresponding ChaptarrNG record;
multiple monitored editions of the same format still fail closed. The
populated-library rehearsal accounted for all 127,177 rows across 39 source
tables; 81 book classifications still required human review, and the test
choices were not reviewed as correct library decisions. A separate 2026-10-18
Docker image-switch smoke used an empty database created by BookshelfNG `.71`:
ChaptarrNG `.977` converted it on normal startup, retained the original database,
and restored it through rollback; the BookshelfNG image started again on port
8787. This does not validate real user data or the Unraid Community Apps flow.

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
available while the release freeze is active. The converter does not accept
PostgreSQL databases or two populated BookshelfNG databases at once. The
BookshelfNG helper archives appdata, but
media/download volumes and settings stored outside appdata need separate
handling. Keep the BookshelfNG service stopped, retain an independent backup,
and follow the
[migration guide](MIGRATE_FROM_BOOKSHELFNG.md).

## Legacy install channels

| Channel | Published ChaptarrNG state | BookshelfNG handoff state |
| --- | --- | --- |
| Docker / GHCR | `ghcr.io/snapetech-org/chaptarrng:0.9.977`, `:latest`, and `:main` resolve to the same published multi-architecture image digest. | Legacy BookshelfNG aliases have not been redirected. Mutable tags affect a later pull/recreate; pinned tags and digests need an operator change. |
| AUR | `chaptarrng-bin 0.9.977-1` is published. | `bookshelfng-bin 0.4.21.71-1` remains on the BookshelfNG payload; the old identity has not been upgraded to ChaptarrNG. |
| Fedora COPR | `chaptarrng 0.9.977-1` built successfully for Fedora 43, 44, 45, and Rawhide x86_64. | The BookshelfNG bridge trigger was skipped. |
| Chocolatey | The `.977` package built and the hosted BookshelfNG-to-ChaptarrNG upgrade smoke passed, but Chocolatey returned HTTP 403 on publish; the live feed has no `chaptarrng` entry. | BookshelfNG `0.4.21.70` remains public. Its identity was not submitted because the bridge is disabled. |
| Unraid | The ChaptarrNG template is maintained separately. | Existing BookshelfNG templates and containers still need an explicit image/template update. |
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

This is source-only and is not in `v0.9.977`. Hosted platform and database
checks passed, and the synthetic external-tool smoke preserved supplied
chapter titles and offsets. Representative-media checks for tags, embedded
chapters, conversion failure, source retention, and restart/resume behavior
remain open. It does not close the Audiobook processing and tags row in
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
   rollback.
3. Resolve the Chocolatey HTTP 403; the `.977` COPR build is verified, while
   each old-identity package build still needs testing;
   verify each published package checksum or image digest against the tested
   artifact.
4. Decide and document handoffs for historical channels that cannot be
   redirected. Preserve a manual migration path for pinned and offline installs.
5. Run the final migration against representative operator data, review every
   ambiguous media choice, and verify external clients against the converted
   library before enabling automatic aliases.

The conversion preview, source-side archive helper, synthetic conversion and
rollback checks, and package templates are present. Full feature parity,
representative real-world rollback, all legacy package publications, and a
universal automatic update path are not established. See the
[BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
and the linked parity roadmap for the detailed evidence ledger.
