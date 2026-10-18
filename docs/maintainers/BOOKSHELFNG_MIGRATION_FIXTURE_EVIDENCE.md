# BookshelfNG migration fixture evidence

**Reviewed:** 2026-10-18. **Scope:** synthetic end-to-end data built by the
published BookshelfNG `0.4.21.71` Linux binary and exercised with an
unpublished ChaptarrNG candidate. This is maintainer evidence, not operator
cutover approval.

## Candidate and fixture

- ChaptarrNG source: draft PR [#131](https://github.com/snapetech-org/chaptarrng/pull/131),
  commit `2334625b4ca57c22c39f09d6780830e4e4f1e79c`.
- Exact-head Build run:
  [39167162488](https://github.com/snapetech-org/chaptarrng/actions/runs/39167162488).
  Linux, macOS, and Windows builds/tests, PostgreSQL migrations, release-note
  validation, and native-runner receipt reconciliation passed. CodeQL,
  dependency review, and dependency/secret scan checks also passed on this
  head. The PR remains draft.
- The Linux artifact from that run reports ChaptarrNG `0.9.978.0`; its
  executable SHA-256 is
  `dfdaae95b9cbd6a4cfe44cfe042f7e6ef87ca60402be00fa8376dc1d9fea1194`.
- Source: the published BookshelfNG `0.4.21.71` Linux x64 release archive,
  SHA-256
  `4197d53b757ebe99642fb8f4213f963dd54641401d1f188ab6daadcb3b024d5d`;
  extracted `Readarr` executable SHA-256
  `0d7c82722b1948cc4d33de139698050944b1f4f1647006836364cd9b736e5f6b`.
- The BookshelfNG fixture helper started that released binary on loopback,
  migrated a disposable database to schema 158, and populated deterministic
  linked records. No production backup, real credentials, external service,
  or real media was used. Fixture credentials and paths are synthetic.

The fixture contains one author, one book/work, two editions (ebook and
audiobook), two book files, one extra file, one metadata sidecar, one series
and link, one user, one notification, one history row, one download-history
row, one blocklist row, two roots, one tag, and six settings. It uses bare
numeric Hardcover IDs as stored by BookshelfNG native Hardcover mode. The
settings cover a fake Hardcover token, match threshold `75`, supplemental
catalogs, per-field source preferences, and a fake Google Books key.

## Runtime results

The following checks used separate disposable copies of that fixture and the
Linux candidate artifact:

- **Preview:** passed. SHA-256 checks showed the source `readarr.db` and its
  WAL were unchanged. SQLite shared-memory state and a conversion lock file
  are transient runtime files, so this statement is limited to the database
  and WAL contents.
- **Explicit apply:** passed with the required
  `-confirm-bookshelf-service-stopped` option. The source was migrated on a
  working copy and retained at schema 158. The ChaptarrNG database reached
  schema 121 and passed `integrity_check` with zero foreign-key errors.
- **Data disposition:** the single source book became separate ebook and
  audiobook catalog records. Both editions and files remained; the extra
  file, metadata sidecar, series relationship, user, notification, history,
  download history, blocklist, roots, and tag were retained. The native
  Hardcover author ID became `hc:9001`, and six provider alias rows were
  created. Ebook/audiobook quality and metadata profile overrides remained
  attached to their formats.
- **Settings:** the native Hardcover credential was mapped to
  `HardcoverApiToken`; the original source setting remained in the retained
  backup. Threshold `75` mapped to `Balanced`, and the original numeric
  setting was retained for audit. The report identifies this as a lossy
  approximation between different matching algorithms. Supplemental catalog
  choices, per-field preferences, and the fake Google Books key were retained.
  BookLore notification settings and triggers were retained unchanged.
- **Restore:** after a synthetic post-migration config write, restore preview
  passed and explicit restore succeeded. The restored source matched the
  original fixture's logical SQLite dump, SHA-256
  `ec9cb0f49c83e30108f7dbe89a0f153b031f3157704acf0fd01fed7da4317477`.
  It returned to schema 158 with the original row counts, passed integrity and
  foreign-key checks, and kept the ChaptarrNG backup containing the converted
  state and post-migration write.
- **Ordinary startup:** passed in a network-isolated sandbox on another fresh
  fixture copy. ChaptarrNG automatically converted the source, retained one
  BookshelfNG backup, reached schema 121, and passed SQLite integrity and
  foreign-key checks. The process was stopped after conversion was observed.
- **Fail-closed cases:** two populated legacy database candidates were
  rejected as ambiguous; schema 159 was rejected as newer than the bundled
  chain; and apply without stopped-service confirmation was refused. Each
  refusal left the source database unchanged.

The successful synthetic runtime evidence shows the current candidate's
SQLite preview, apply, startup conversion, data mapping, refusal, and rollback
paths working together for this fixture. It does not establish correct media
classification for ambiguous real records, PostgreSQL operator-data safety,
all historical schemas, provider/client continuity, real installation or
updater behavior, or feature parity. Do not use this candidate for operator
data. ChaptarrNG `v0.9.977` remains the latest public release, and releases,
packages, images, and compatibility bridges remain frozen.

See [retirement status](../BOOKSHELFNG_RETIREMENT.md), the
[operator migration guide](../MIGRATE_FROM_BOOKSHELFNG.md), and the
[PostgreSQL candidate notes](../BOOKSHELFNG_POSTGRESQL_MIGRATION.md) for the
published limits and remaining acceptance gates.
