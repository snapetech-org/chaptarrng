# Move a BookshelfNG installation to ChaptarrNG

> **Release status (2026-10-19).** ChaptarrNG `v0.9.977` is still the latest
> published release. Releases are frozen until the full BookshelfNG retirement
> acceptance gates close; a source change or version bump is not a release.
> The published `.977` converter supports read-only preview for BookshelfNG
> `0.4.21.71` with a migration-158 SQLite database. Migration is not approved
> for production cutover yet: do not apply it, install a bridge package, or
> start ChaptarrNG against BookshelfNG appdata. Keep BookshelfNG in service
> until the acceptance gates close. The `.71` source helper can archive
> appdata and invoke migration commands, but its availability does not grant
> cutover approval; see the
> [BookshelfNG operator guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md).
> For an immutable Docker deployment, use
> `ghcr.io/snapetech-org/chaptarrng:0.9.977`.
> The release bundles the BookshelfNG source migration chain and supports the
> exact migration-158 SQLite schema. Its pre-upgrade check rejects schemas
> before migration 14; upgrade those installations to the final BookshelfNG
> `0.4.21.71` release first. A later source-only fix passed hosted checks on
> `main`, but is not in `.977` and is not available to operators during the
> release freeze.
> **Provider identity warning:** `.977` maps bare numeric source IDs to
> Goodreads regardless of `HARDCOVER`. A database from the native Hardcover
> BookshelfNG mode can therefore pass integrity checks while acquiring the
> wrong provider identity. Do not apply `.977` to a native Hardcover database;
> its read-only preview cannot validate provider identity either. The
> unpublished candidate requires an explicit source mode, but remains
> unsupported for operator cutover during the release freeze.
> On 2026-10-19, candidate build `0.9.978.0` passed preview, apply, rollback
> preview, and explicit rollback on a disposable copy of populated
> migration-158 SQLite data with `HARDCOVER=true` and network access disabled.
> Migration preview left the source unchanged. Apply produced a schema-121
> ChaptarrNG database and retained the schema-158 source backup. Rollback
> preview left the converted database and source backup unchanged; explicit
> rollback restored a byte-identical source backup and preserved the converted
> database separately. Integrity and foreign-key checks passed, and the
> original private snapshot remained unchanged. This is one candidate round
> trip; it does not close ambiguous
> media review, older-schema or PostgreSQL operator-data acceptance, feature
> parity, or installer/channel handoff.
> Draft ChaptarrNG [PR #131](https://github.com/snapetech-org/chaptarrng/pull/131)
> contains this candidate. Previous exact-head run
> [39182438062](https://github.com/snapetech-org/chaptarrng/actions/runs/39182438062)
> passed on PR head `bdd7518`; the current importer and edition-catalog
> follow-up still needs exact-head hosted validation. Local focused importer
> and edition-catalog tests pass 44/44. See the
> [fixture evidence](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md).
> PostgreSQL source conversion remains unsupported in published releases.
> Pinned images,
> native packages, and updater channels need separate upgrade steps; database
> conversion alone does not update an installation channel.
> See [BookshelfNG retirement status](BOOKSHELFNG_RETIREMENT.md) for live
> package/channel state and the remaining acceptance gates.

An approved future migration will use the standard ChaptarrNG application and
container build. The `Chaptarr` executable provides explicit preview and apply
commands, and normal startup also detects and converts a supported BookshelfNG
database automatically. Do not run normal ChaptarrNG startup against
BookshelfNG appdata while cutover remains unapproved.

Migration selects one supported database per appdata directory. If both
`readarr.db` and `audioarr.db` are non-empty in the same directory, the current
source resolver stops with an ambiguity error; migration does not merge them
or pick one silently. Keep independently operated installations in separate
appdata directories and migrate each one separately. The published BookshelfNG `.71`
helper may print a zero-byte `readarr.db` beside a populated `audioarr.db`;
ChaptarrNG
`.977` independently resolves the sole populated legacy database. BookshelfNG
PR #154 fixed the helper's candidate selection on source `main`, after `.71`;
that fix is not in the published helper. If both files belong to
data stored together, preserve the original directory and resolve database
ownership and shared settings before making separate working copies.
If you want one combined library, consolidate tracked media into the
BookshelfNG instance you are keeping with the
[two-instance consolidation runbook](https://github.com/snapetech-org/bookshelfng/blob/main/README.md#one-instance-for-ebooks-and-audiobooks),
verify that library, and then migrate that instance. The runbook does not merge
database histories, profiles, monitoring, or settings.

The published `v0.9.977` converter supports BookshelfNG migration-158 SQLite
databases and bundles the BookshelfNG source migration chain. It upgrades a
recognized source schema on a disposable copy, keeps the untouched snapshot
for rollback, then stages a fresh native Chaptarr database, accounts for every
source-table row, checks SQLite integrity and foreign keys, and promotes only
after validation. The public `.977` release rejects schemas before migration
14 because its table guard sees the pre-rename `Blacklist` name. PR #126 merged
a version-aware allowlist fix and synthetic migration-1 rollback fixture into
`main`; hosted Linux, macOS, Windows, and PostgreSQL migration checks passed.
That source fix is not in `.977`. Populated synthetic v22 coverage exercises
conversion and rollback, but it is not a real-data rehearsal or proof for every
historical BookshelfNG release. Unknown or newer schemas fail closed without
changing the source.

If BookshelfNG is older than `0.4.21.71`, update it to that final release and
let BookshelfNG finish its own database upgrade before switching applications.
This brings the source to migration 158, which the published `.977` converter
accepts. For databases earlier than migration 14 that cannot be upgraded with
BookshelfNG, do not switch to ChaptarrNG `.977`; the source fix is unreleased
while the retirement freeze is active. Stop the service after it reaches an
idle state, then run ChaptarrNG preview. The published `.977` converter does
not support PostgreSQL BookshelfNG databases. This work branch contains an unreleased candidate for one PostgreSQL migration-158 schema. PostgreSQL remains unsupported for operators; see the [PostgreSQL candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md). Do not use the candidate for operator data; the published migration path remains SQLite-only. See the
[PostgreSQL candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md).

Environment variables are outside the database conversion. Preserve the
BookshelfNG diagnostics variables if that optional module was explicitly
enabled; ChaptarrNG honors that choice only when the archived appdata also
contains the old diagnostics module marker. See the
[optional diagnostics guide](OPTIONAL_DIAGNOSTICS.md).

BookshelfNG notification rows are copied with their provider name, settings,
credentials, and event triggers. A BookLore row remains connected to its
saved BookLore URL and account; migration cannot assume that these settings
identify a separate BookOrbit server. The current source candidate adds
BookOrbit as a separate Book Dock destination. The unpublished retirement
candidate's proxy tests cover authenticated streaming, redirect refusal,
rejected credentials and files, and partial-batch handling. A live contract
check against an isolated BookOrbit v3.2.0 instance passed setup, login,
permission checking, EPUB upload/listing, and unsupported-file rejection.
This checks upload admission, not BookOrbit's later processing of a production
book or a real-user handoff. Configure and test a separate BookOrbit
connection in **Settings > Connect** after migration, then keep or disable the
legacy BookLore connection as needed. This source candidate is not in published
`v0.9.977` while the release freeze remains active.

Preserve `HARDCOVER=true`, `HARDCOVER_AUTH`, `HARDCOVER_API_KEY`, and any
custom `HARDCOVER_API_URL` environment value used by the BookshelfNG install.
Run preview and migration with the same `HARDCOVER` and `HARDCOVER_NATIVE`
values as the source install. When native Hardcover mode is active, BookshelfNG
stores many primary provider IDs as bare numbers; the converter uses
`HARDCOVER=true` (unless `HARDCOVER_NATIVE=false`) to preserve those as
Hardcover identities. Set `HARDCOVER=false` for a Goodreads-primary source.
If `HARDCOVER` is unset or invalid, conversion now fails closed when it reaches
a bare numeric ID instead of silently assigning Goodreads identity. Preview
reports the selected interpretation for authors, books, editions, and series.
Changing the mode during conversion can associate every bare numeric ID with
the wrong provider.

The current ChaptarrNG work-branch candidate also maps a saved `HardcoverAuth`
database value to `HardcoverApiToken`; a saved ChaptarrNG token takes
precedence, and the original BookshelfNG database remains available in the
rollback backup. That native provider and token mapping are not in published
`v0.9.977`. PR #131 remains draft; check its current exact-head results. Representative
converted-data acceptance remains open; see the [PostgreSQL candidate
notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md).
Do not treat this as supported migration behavior during the release freeze.

BookshelfNG's `BOOKSHELF_M4B_MERGE`,
`BOOKSHELF_M4B_AAC_BITRATE_KBPS`, and `BOOKSHELF_FFMPEG_PATH` variables are
also outside database conversion. The published ChaptarrNG `v0.9.977` does not
honor them. ChaptarrNG PR #129 merged the compatibility implementation to
`main` as `949ae3dd`, but it is not in `.977`; keep `BOOKSHELF_M4B_MERGE` unset
until a build with that support is available and the remaining acceptance
gates close. The current retirement candidate's
`real_m4b_tool_should_merge_and_import_legacy_multipart_audiobook_when_installed`
test passes in a tool-enabled Linux container with `m4b-tool` 0.5.2, `mp4v2`
2.1.1, and FFmpeg/FFprobe 6.1.1. It generates two MP3 tracks and imports them
through the real converter, verifying tag-based track order, chapter titles
and offsets, output metadata, library registration, Move-mode source cleanup,
and Copy-mode source retention. Four failure cases verify fallback to
separate original-track imports in Move and Copy modes: two make the second
source unreadable to FFprobe, while two use a controlled FFmpeg error to make
the real `m4b-tool` process fail. Representative files, embedded-chapter
compatibility, and restart/resume behavior remain open.
The implementation requires the FFmpeg executable to be named `ffmpeg`
(`ffmpeg.exe` on Windows) for `m4b-tool` to find it through `PATH`.

The conversion reuses the existing data directory and `config.xml`, preserving
the saved HTTP port, URL base, API key, and server settings. Keep the same media
and download mounts and host/container port mappings. This preserves existing
SeerrNG and reverse-proxy endpoints when those services used the old address.

## Recovering unmapped files

The published `v0.9.977` release retries unmapped files against the local
library and Chaptarr's V5 matcher. ChaptarrNG PR #127 merged configured-catalog
recovery to `main` as `b3089a39`; that source change is not in `.977`. It
searches after those paths fail and
searches from an exact ISBN, ASIN, or provider-ID tag (including Goodreads IDs),
or from one unambiguous book title and author tag. Every supplied identity field
must match the result, including normalized title and author when present. The
result must use the same media type and have stable provider work and author
IDs. Conflicting or ambiguous results stay unmapped. A single result is sent
through the existing pending-author-import flow; catalog search never moves or
attaches a file directly. This source change does not change release or
channel acceptance status.

## Matching setting compatibility

The published ChaptarrNG `v0.9.977` converter preserves the legacy
`BookImportMinimumMatchPercent` row but does not apply it. The current source
importer retains that row and, when the source has no explicit
`BookMatchingStrictness`, maps the closest policy band:

| BookshelfNG percentage | ChaptarrNG mode |
| --- | --- |
| 50–66 | Aggressive |
| 67–82 | Balanced |
| 83–99 | Strict |

An explicit ChaptarrNG mode takes precedence. The migration report names the
mapping and the original numeric row remains for audit. Values outside 50–99
stop conversion before promotion. This is a lossy policy translation: the
applications use different fuzzy-title and import-scoring algorithms. Review
the chosen mode against expected behavior on converted data; do not treat it as
an exact recreation of BookshelfNG matching. This source change is not in
`.977` while the release freeze remains active.

## Read-only preview and gated apply path

Stop BookshelfNG first and make an independent copy of the entire data
directory. Run the preview with the **same regular ChaptarrNG release** that
will run the migrated service. Preview performs the full conversion into a
temporary staged database, prints the row disposition for every source table,
and leaves the source database untouched.

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.977 \
  dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config
```

Some source records have no edition rows, so their media type cannot be
determined from the database. A monitored mixed-format book can also have no
selected edition. With no media map, preview and startup now preserve each
unresolved book in both ChaptarrNG catalogs and leave both records unmonitored.
This keeps the migration moving without starting speculative searches in the
wrong format. It can create a temporary duplicate title across the ebook and
audiobook catalogs; no files are assigned or moved for that book. The fallback
also applies to a monitored mixed-format book with no selected edition: its
existing editions and files remain, but neither format is monitored until the
operator reviews it.

The converter writes
`readarr.db.bookshelf-media-review.json` beside the database and keeps it after
automatic conversion. It lists the source book IDs, titles, authors, original
monitoring state, reason, and the `both-unmonitored` fallback. On Unix it is
created with owner-only permissions because it contains library metadata. Use
the review file after startup to correct the media type and monitoring choice
in ChaptarrNG. The normal startup log gives the review file path. Automatic
preservation prevents a conversion stop; it does not infer the book's true
format.

When one audiobook edition and one ebook edition are selected for the same
BookshelfNG work, conversion preserves each selection on its matching
ChaptarrNG record. If two or more editions of the same format are selected,
conversion stops before promotion because ChaptarrNG has one selected-edition
field per format record. Correct that selection in BookshelfNG and preview
again; the source database remains available for another attempt.

To choose formats during conversion, set every `mediaType` to `audiobook`,
`ebook`, `both`, or `both-unmonitored`, then save the completed document as
`readarr.db.bookshelf-media-map.json` beside `readarr.db`. For a book without
editions, the map creates the corresponding ChaptarrNG media record or records.
For a monitored mixed-format book without a selected edition, the map controls
which existing formats are monitored while preserving all source editions and
files. `both-unmonitored` retains both formats without starting searches. Run
preview again with the map in place. Normal startup uses the same map and
converter, so no separate migration build is needed.

The map is tied to source book IDs and the current ambiguous-record set. If the
database changes after review, preview rejects missing entries and stale
choices. It also rejects map entries for books that are no longer ambiguous,
including when the current source needs no map; remove stale entries or use the
updated review file, then preview again instead of guessing.

Once the retirement gates close and apply is approved, either let normal
startup apply conversion or run the explicit apply command once:

> **Do not run this apply command or normal startup against BookshelfNG data
> during the current release freeze.** The migration and rollback commands are
> implemented, but operator-data, parity, and channel acceptance remain open.

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.977 \
  dotnet /app/Chaptarr.dll -migrate-bookshelf -confirm-bookshelf-service-stopped -data=/config
```

Apply retains a backup beside the original database. Keep the independent
directory copy and generated database backup until you have checked the
library, root paths, profiles, indexers, download clients, notifications, and
SeerrNG connection. The explicit stopped-service flag makes the required
quiescent data directory an operator-confirmed precondition.

## Update the local MCP connection

If `snapetech-mcp` is configured for BookshelfNG, change the MCP host to use
the ChaptarrNG app entry and URL after migration:

```text
SNAPETECH_MCP_APPS=chaptarrng
SNAPETECH_MCP_CHAPTARRNG_URL=<ChaptarrNG base URL>
SNAPETECH_MCP_CHAPTARRNG_API_KEY=<dedicated ChaptarrNG service key>
```

Configure that credential in ChaptarrNG as
`CHAPTARR__AUTH__SEERRAPIKEY`, then restart the MCP process. The BookshelfNG
entry uses a media-type selector; ChaptarrNG serves both media types through
one entry, so remove `SNAPETECH_MCP_BOOKSHELFNG_MEDIA_TYPE`. The current MCP
tools read status, paged books, bounded catalog search, queue, and history;
catalog search may contact the configured metadata providers. See the
[SnapeTech MCP guide](integrations/snapetech-mcp.md) for access and data
boundaries.

## Roll back after ChaptarrNG has started

Stop every process using the data directory. First validate the retained
BookshelfNG backup without changing either database:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.977 \
  dotnet /app/Chaptarr.dll -preview-bookshelf-restore \
    -bookshelf-backup=/config/readarr.db.bookshelfng-backup-YYYYMMDDTHHMMSSfffZ-ID.db \
    -data=/config
```

After preview passes, apply the restore while all services remain stopped:

```sh
docker run --rm \
  -v /host/path/to/bookshelfng-config:/config \
  ghcr.io/snapetech-org/chaptarrng:0.9.977 \
  dotnet /app/Chaptarr.dll -restore-bookshelf \
    -confirm-bookshelf-service-stopped \
    -bookshelf-backup=/config/readarr.db.bookshelfng-backup-YYYYMMDDTHHMMSSfffZ-ID.db \
    -data=/config
```

Rollback validates the current ChaptarrNG database and exact supported
BookshelfNG migration-158 backup before changing files. It preserves the
current ChaptarrNG database, including writes made since conversion, as
`readarr.db.chaptarrng-backup-<timestamp>-<id>.db`; the original
`readarr.db.bookshelfng-backup-*` remains untouched. Start only BookshelfNG
after restore. If promotion validation fails, the tool restores the prior
ChaptarrNG database and preserves the failed restore candidate for inspection.

## Unraid

> **Not approved for cutover yet.** [BookshelfNG Unraid PR #5](https://github.com/snapetech-org/bookshelfng-unraid/pull/5) contains a draft
> template handoff; its Community Applications upgrade and rollback flow has
> not passed acceptance. Do not change the template or start ChaptarrNG against
> the BookshelfNG appdata until the retirement gates close.

Stop BookshelfNG and back up the full appdata directory. In **Apps > Previous
Apps**, apply the updated BookshelfNG template. It points to
`ghcr.io/snapetech-org/chaptarrng:latest`, the same image used by fresh ChaptarrNG
installs, while retaining the old `/config`, `/data`, `/download`, port,
PUID/PGID, timezone, and Hardcover settings. Starting the container runs the
regular ChaptarrNG app and its automatic migration. The old `config.xml` keeps
the saved app port and URL base.

The candidate template keeps `HARDCOVER=true` and `HARDCOVER_NATIVE=true` for
the native Hardcover configuration it was built for. Preserve
`HARDCOVER=false` for a Goodreads-primary source; the migration requires the
same explicit mode during preview, automatic startup, and apply. The template
XML and settings-preservation script passed locally, but the actual Community
Applications update and rollback flow has not been run.

Wait for the container health check, open the existing URL, and inspect
**System > Logs**. The backup filename starts with
`readarr.db.bookshelfng-backup-`. Check the SeerrNG service connection and run
its diagnostic if the migration changed any network address or API key.

If startup fails, keep both the original copy and the retained source backup.
Stop ChaptarrNG, restore the independent appdata copy, then reinstall the old
BookshelfNG image. Do not start BookshelfNG against a ChaptarrNG-converted
database.

## Docker Compose

> **Not approved for cutover yet.** Do not change the image or start ChaptarrNG
> against BookshelfNG appdata until migration and rollback acceptance close.

Change the image to ChaptarrNG and retain the old host paths, container mount
targets, and port mapping. For a manual cutover, set `HARDCOVER=true` for a
native Hardcover BookshelfNG source or `HARDCOVER=false` for a Goodreads-primary
source. The compatibility image targets being added to the release workflow
will set the matching mode for the legacy mutable aliases; they have not been
published or enabled while the release freeze is active. For an installation
that used the default BookshelfNG port, the shape is:

```yaml
services:
  chaptarrng:
    image: ghcr.io/snapetech-org/chaptarrng:latest
    container_name: chaptarrng
    environment:
      PUID: "99"
      PGID: "100"
      TZ: Etc/UTC
      HARDCOVER: "true" # use false for a Goodreads-primary BookshelfNG source
    volumes:
      - /host/path/to/bookshelfng-config:/config
      - /host/path/to/media:/data
      - /host/path/to/downloads:/download
    ports:
      - "8787:8787"
    restart: unless-stopped
```

The saved BookshelfNG `config.xml` keeps the internal port at `8787`; the
published ChaptarrNG image still defaults to `8789` for fresh installs. If the
old instance used a different port, preserve that port in both its container
configuration and host mapping.

## Unpublished candidate populated SQLite round trip (2026-10-19)

The unpublished ChaptarrNG PR #131 candidate (build version `0.9.978.0`)
completed a full preview/apply/restore rehearsal on a disposable copy of a
private populated BookshelfNG migration-158 SQLite database. The BookshelfNG
service was not changed, the source snapshot stayed byte-identical, and the
candidate ran with `HARDCOVER=true` and network access disabled.

- Migration preview passed without changing the source database.
- Explicit apply passed after the stopped-service confirmation. The converted
  database reached ChaptarrNG schema 121 and passed SQLite integrity and
  foreign-key checks; the original schema-158 backup remained retained.
- Rollback preview passed without changing either the converted database or
  the source backup.
- Explicit rollback restored a byte-identical copy of the retained schema-158
  source backup and preserved the converted schema-121 database as a separate
  ChaptarrNG backup.

The private library contents, counts, identifiers, and hashes are omitted.
This validates one populated SQLite migration and restore path. It does not
classify ambiguous formats, prove every data mapping or client behavior, or
close older-schema, PostgreSQL, full feature-parity, or installation-channel
acceptance. Keep the release freeze and production cutover restrictions in
place; the candidate is not a published migration tool.

## Historical populated-database rehearsal (2026-10-14)

The ordinary Linux/amd64 ChaptarrNG image was exercised against an isolated
copy of a real BookshelfNG migration-158 SQLite database. Preview accounted
for all source tables and rows and left the source database hash
unchanged. At the time, that earlier candidate required a complete per-book
media map before preview could pass. The same standard image then completed
explicit apply on one copy and normal first-start conversion on another. No
compatibility image or build was used, and the live BookshelfNG service was
not changed.

The applied database reached native migration v119. Its retained backup
matched the source table definitions and rows exactly; both databases
passed SQLite integrity and foreign-key checks. The importer retained source
file rows with `EditionId=0`, BookshelfNG's intentional unmapped-file
sentinel, without inferring an edition, book, or media type. A focused fixture
also verifies that explicit sidecar and history links to such a file remain
attached correctly. Volatile command queue/execution, provider health/cursor,
scheduled-task, and source `VersionInfo` state is deliberately recreated;
acquisition history is migrated.

Normal startup kept the saved port, URL base, API key, and SSL settings,
advertised Docker image-pull updates, retained a source backup, served the
existing port's ping and UI routes, and returned all four configured indexers
with their runtime-derived RSS and search capabilities. The conversion logs
and database copies were kept in a private VM scratch directory.

The real database exposed books whose media choice could not be derived from
source relationships. Earlier test maps used explicit technical choices
only; those choices were not reviewed as correct library classification. The
published `v0.9.977` release keeps unresolved records in both catalogs
without monitoring and retains a private review file. Operators still need to
confirm the true format and monitoring intent for those records. A map can
apply that review before conversion; otherwise correct the records after
startup. The fallback removes the startup blocker, but it does not establish
semantic parity for those books.

This proves one populated migration-158 SQLite path on Linux/amd64. It does not
prove older BookshelfNG schemas, the PostgreSQL candidate, every provider or
notification setting, representative M4B conversion, Windows-native
conversion, package-channel upgrades, rollback against a real populated deployment, populated SeerrNG
requests, or complete feature parity. Feature, installation, and distribution
channels remain separate continuity checks.

## Published-image populated SQLite round trip (2026-10-18)

The public ChaptarrNG `v0.9.977` Linux/amd64 image was also tested against a
private populated migration-158 SQLite snapshot. The live BookshelfNG service
and database were not used as the test target. Preview completed with the
snapshot database bytes unchanged and created no migration backup. It produced
private media-review artifacts; no media map was supplied, so ambiguous format
choices remain for operator review.

Explicit apply on a separate copy reached native Chaptarr migration v120 and
retained the migration-158 source backup. The source backup matched the input
snapshot's logical database contents. The converted database and retained
source backup both passed SQLite integrity and foreign-key checks. On that
copy, restore preview passed, and explicit rollback restored migration 158
with the same logical database contents as the source snapshot. Rollback kept
the migration-120 ChaptarrNG database as a separate backup matching its
pre-restore contents; both databases passed integrity and foreign-key checks.
A second isolated restore-preview check left both database files byte-for-byte
unchanged and created no ChaptarrNG backup artifact.

This adds populated SQLite apply and rollback evidence for one migration-158
database. It does not resolve ambiguous media classifications or establish
older-schema, PostgreSQL, Windows-native, package-channel, external-client, or
full feature-parity acceptance. No user data or database files were committed.

An earlier hosted Windows run of the migration candidate failed four
promotion/rollback cases. The Linux image checks below do not establish a
passing native Windows migration run.

## Published image smoke test (2026-10-15)

The public standard `v0.9.971` Linux/amd64 image
([release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.971))
was exercised against a populated synthetic database built from the checked-in
BookshelfNG migration-158 schema and importer fixture: one author, one source
book, two editions, two files, and two history rows.

Preview left the source database bytes unchanged and created no backup. The
explicit apply command converted the data to native migration v120, retained
the v158 source backup, and passed SQLite integrity and foreign-key checks.
After adding a configuration row as the image's default service user (UID 99),
rollback restored the v158 source and retained the newer ChaptarrNG database,
including that write, in its ChaptarrNG backup.

On a separate copy, the ordinary container startup command detected and
converted the same source automatically, retained its backup, passed the
container health check, and passed SQLite integrity and foreign-key checks.
A v157 preview was rejected with the source bytes unchanged and no backup or
promotion. These checks exercise the published Linux image and synthetic
fixture; they do not replace validation against an operator's own data or prove
support for another schema or database engine.

A separate fixture included a monitored source book with no editions. Preview
stopped without changing the database and wrote the review file with mode
`0600`, owned by the container's default UID 99. After the operator map chose
`both`, preview passed and apply created audiobook and ebook records with the
source monitoring state, removed the review file, retained the source backup,
and passed SQLite integrity and foreign-key checks.

## Published image smoke test (2026-10-17)

The public `ghcr.io/snapetech-org/chaptarrng:0.9.974` image was exercised
against three isolated copies of the checked-in migration-158 fixture (39
source tables). The tested `latest` tag had the same per-platform manifest
digests as `0.9.974` and reported application version `v0.9.974.0` at release
commit `53a265f0`.

Preview left the source database hash unchanged and created no backup. Explicit
apply completed and retained the BookshelfNG backup. Rollback preview passed.
After writing a setting to the converted ChaptarrNG database, rollback restored
the BookshelfNG v158 source while retaining the later ChaptarrNG write in a
separate backup. On a third copy, normal startup detected `readarr.db`,
converted it to the native Chaptarr schema, and started the service.

The fixture has no `/library` mount or desktop browser, so normal startup also
logged the expected missing-root and headless-browser warnings; those did not
stop conversion or service startup. This validates the current public image's
migration, rollback, and automatic startup paths against the synthetic fixture.
It does not establish behavior for older schemas, PostgreSQL source
conversion, live operator data, every BookshelfNG feature, or every package
and update channel.

## Published image smoke test (2026-10-18)

On an isolated Linux/amd64 Docker host, the published BookshelfNG `main` image reported version `0.4.21.71` and created an empty migration-158 SQLite database with 39 tables. After stopping it, ChaptarrNG `v0.9.977` started against the same `/config`, `/data`, and `/download` mounts. Its normal startup converted the database to Chaptarr migration 120, retained the untouched BookshelfNG backup, and listened on the saved port 8787. The converted database and source backup both passed SQLite integrity checks.

Rollback preview and apply passed. The restored BookshelfNG database remained at migration 158 with 39 tables, and the previous BookshelfNG image started against it on port 8787. A test-only setting written after conversion remained in the retained ChaptarrNG backup. This was an empty-database Docker rehearsal; it does not validate populated operator data or the Unraid Community Apps template update flow.

## Provider-mode image-switch and rollback (2026-10-18)

A follow-up rehearsal used a populated synthetic database served by the public
BookshelfNG `.71` Hardcover image. It exposed that starting the prior Chaptarr
candidate without `HARDCOVER=true` could misclassify bare numeric provider IDs
as Goodreads IDs. With the source mode preserved, migration kept author,
book, and edition IDs in Hardcover fields, passed database integrity checks,
and served the converted API records. Rollback restored the source database
byte-for-byte, retained the converted database separately, and the `.71`
Hardcover image reopened the restored source. The current source candidate now
fails closed on bare IDs when the mode is unset or invalid, and adds separate
compatibility image targets for the legacy Goodreads and Hardcover aliases.
Exact-head run [39175250100](https://github.com/snapetech-org/chaptarrng/actions/runs/39175250100)
passed the provider-mode regression and PostgreSQL fixture checks. The final
Linux artifact refused conversion without a provider mode, preserved Goodreads
and Hardcover identities with their explicit modes, converted during normal
startup, and restored the source byte-for-byte during rollback. Local BuildKit
target checks and hosted alias/bridge regression scripts also pass; the actual
GHCR compatibility images and alias dispatch have not been built or published.
This synthetic rehearsal does not establish operator-data or installer
acceptance. See the detailed
[fixture evidence](maintainers/BOOKSHELFNG_MIGRATION_FIXTURE_EVIDENCE.md).

## AUR package candidate rehearsal (2026-10-18)

In an isolated Arch container, the active BookshelfNG `.71` systemd service was
upgraded through the local BookshelfNG-identity `.977` package candidate. The
pacman hooks stopped and restarted the service; ChaptarrNG startup converted a
synthetic populated migration-158 database to migration 120 and retained the
source backup. Restore preview and apply returned it to migration 158, retained
the converted database, and passed SQLite integrity and foreign-key checks.
Pacman downgrade restored `.71`, and the BookshelfNG service started against the
restored database. The config override and synthetic data canary survived the
transition. `systemd-networkd-wait-online` was masked only in the test container
because its network was disconnected. This validates the candidate's synthetic
service and database path, not operator data or host-specific settings; the
candidate remains unpublished. See the [package test record](../packaging/README.md).

## Limits and update channels

The published `v0.9.977` converter accepts SQLite databases at BookshelfNG
migration 158 and bundles the source migration chain. Its table guard rejects
versions before migration 14 because the chain has not yet renamed `Blacklist`
to `Blocklist`. PR #126 merged the version-aware fix into `main`, with hosted
migration checks passing, but the fix is not in the published release.
Populated synthetic v22 coverage exercises conversion, rollback, and
non-mutating preview. This does not establish older-schema behavior on real
operator data or PostgreSQL source conversion. The PostgreSQL candidate remains unavailable to operators and unpublished; see the [candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md).
The published SQLite converter can preview a migration-158 source, but apply
is not approved during the current release freeze. Do not start normal
ChaptarrNG against BookshelfNG appdata: startup automatically converts a
supported source database. Keep BookshelfNG in service until the acceptance
gates close and a supported cutover is announced.

The public ChaptarrNG `v0.9.977` release contains the converter and standalone
archives. The version-1-to-13 table-alias fix passed hosted validation on
`main` but remains unavailable in the public release during the freeze.
Current package and image-channel status is tracked in
[`packaging/README.md`](../packaging/README.md). The migration bridge is
disabled: existing BookshelfNG package IDs and mutable image aliases do not
currently redirect to ChaptarrNG. Pinned tags, digests, offline installs, and
third-party deployment definitions always require an operator update. A
mutable tag only affects a later pull or container recreation.

BookshelfNG's in-app updater still uses its existing upstream update service;
Snapetech cannot redirect that service. The practical handoff paths are a
manual image/package change or a separately validated legacy-identity bridge.
The bridge must preserve each channel's app-data path and service settings and
must be tested as an upgrade, including startup conversion and rollback, before
it is enabled.

A local Fedora 44 RPM transaction upgraded the published BookshelfNG `.71`
package to a `.977` bridge candidate and downgraded back while preserving
config/data canaries. The candidate used the legacy package identity; its
`systemctl` scriptlet checks were mocked and RPM dependencies were bypassed.
The current legacy COPR repo has no package entries, so this does not validate
installation through COPR/DNF, a real systemd service, or database migration.
See the [package-channel status](../packaging/README.md).

Database conversion is only one retirement requirement. ChaptarrNG does not
yet provide full BookshelfNG behavior parity. Follow the separate
[BookshelfNG parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md)
for open feature, integration, and distribution work, including SeerrNG and
client behavior. Do not treat successful database conversion as proof that a
BookshelfNG deployment can be retired without losing behavior.
