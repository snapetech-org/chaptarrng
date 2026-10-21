# Move a BookshelfNG installation to ChaptarrNG

> **Release status (2026-10-21).** ChaptarrNG `v0.9.977` remains the latest
> public release. Releases are frozen until the BookshelfNG retirement gates
> close. Keep BookshelfNG in service; do not apply migration, install a bridge,
> or start ChaptarrNG against BookshelfNG appdata.
>
> The published `.977` migration path supports BookshelfNG `0.4.21.71` with a
> migration-158 SQLite database. It does not safely identify native Hardcover
> provider IDs; do not apply `.977` to a database from native Hardcover mode.
> Newer SQLite migration, provider compatibility, and installer work exists on
> [ChaptarrNG `main`](https://github.com/snapetech-org/chaptarrng/tree/main),
> but remains unpublished and is not approved for cutover.
>
> Current `main` also upgrades supported older BookshelfNG PostgreSQL schemas
> on a disposable schema snapshot and preserves the untouched source for
> rollback. Its local synthetic PostgreSQL 17 fixture passes conversion and
> rollback from source versions 1, 13, 22, and 33. This capability is not in
> `.977`; representative operator data, independent restore, installed rollback,
> and channel acceptance remain open. See the
> [PostgreSQL candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md).
>
> Current ChaptarrNG [main](https://github.com/snapetech-org/chaptarrng/tree/main)
> also implements BookshelfNG issue [#114](https://github.com/snapetech-org/bookshelfng/issues/114):
> the author-path initialization setting is opt-in and disabled by default.
> Migration carries the saved choice and maps configured ebook/audiobook roots
> to ChaptarrNG defaults when those defaults are empty. Existing author paths and
> files are untouched. Current source restores whole-author move updates,
> paginates Goodreads series imports, and improves Manual Import's author, book,
> edition, and quality selectors with empty-state guidance, retry, stale-choice
> protection, and disabled submission when quality choices are empty. The
> Add New and interactive searches expose retry for provider errors; narrator
> discovery and organize, retag, and convert previews offer announced recovery.
> Manual Import, File Browser, activity lists, author history, book-edit edition
> loading, book-file details and quality loading, Calendar, backups, Wanted pages,
> and conversion settings also retry failed loads. Startup errors identify the
> failed data source and retry failed setup requests when browser storage works.
> The frontend CJS suite passes
> 122/122 locally. Focused migration, metadata,
> move, and CLI checks are listed in the retirement status. Current application
> source is on [ChaptarrNG main](https://github.com/snapetech-org/chaptarrng/tree/main).
> Exact-head CI status for current `main` is tracked by the [Build ChaptarrNG workflow](https://github.com/snapetech-org/chaptarrng/actions/workflows/build.yml). Current source remains unpublished. The source also
> preserves BookshelfNG's optional
> `MoveExtraFilesOnRename` setting, exposes it in Media Management, and includes
> it in settings backups. When enabled, it moves only configured loose files
> after all renamed book media leaves the old folder. The current path audit
> is revision-pinned in the [canonical BookshelfNG consolidation
> report](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/chaptarr-consolidation.md);
> path decisions do not establish feature parity. No release, package,
> image, or migration bridge has been published. Representative operator data, rollback, provider
> behavior, and installed update-channel acceptance remain open. See the
> [BookshelfNG retirement issue](https://github.com/snapetech-org/bookshelfng/issues/144)
> and [operator migration guide](https://github.com/snapetech-org/bookshelfng/blob/main/docs/migrate-from-bookshelfng-to-chaptarrng.md)
> for current gates. The validation notes below are dated evidence snapshots,
> not authorization to cut over.

An approved future migration will use the standard ChaptarrNG application and
container build. The `Chaptarr` executable provides explicit preview and apply
commands, and normal startup also detects and converts a supported BookshelfNG
database automatically. Do not run normal ChaptarrNG startup against
BookshelfNG appdata while cutover remains unapproved.

The ChaptarrNG source also includes
[`scripts/bookshelfng-to-chaptarrng.sh`](../scripts/bookshelfng-to-chaptarrng.sh),
a host-side helper for Docker installations. Each action first creates a
mode-`0600` archive of the full appdata directory in a separate backup
directory. The helper supports `snapshot`, migration `preview` and `apply`,
and restore `preview` and `rollback`. It rejects ambiguous legacy databases,
requires an explicit Goodreads or native Hardcover choice for migration,
accepts only a versioned ChaptarrNG image or digest, and requires explicit
stopped-service confirmation before apply or rollback. Native Hardcover
migration requires a versioned `v0.9.978` or newer image; published `.977`
does not support that identity conversion. The release workflow is configured
to include the helper in future Linux standalone archives after a release is
approved. No new release or cutover is authorized by this source change.

The Add New page carries over the BookshelfNG “search as you type” choice when
you open ChaptarrNG from the same browser origin. If you move to a different
host, scheme, or port, browser storage is separate; set the option on the
ChaptarrNG Add New page there.

Series rows include the BookshelfNG series pack search action. Grabbed packs
retain their series books for ChaptarrNG's existing Manual Import review; check
the file-to-book assignments before importing a pack.

Interactive search checks the selected torrent client for a matching info hash.
If it finds one, ChaptarrNG offers to adopt that existing item into its tracked
queue without starting a second download. Confirm the match before adopting it.

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
[39292223717](https://github.com/snapetech-org/chaptarrng/actions/runs/39292223717)
on Linux, macOS, and Windows, including PostgreSQL migration smoke,
release-note contract, native-runner receipt reconciliation, and the Linux
pinned-tool M4B smoke. CodeQL, security/secret scan, dependency review, and
NuGet submission passed; disposable-fork validation was skipped by its gate.
This validates application code only. PR #131 later merged on 2026-10-20.
It does not authorize production conversion or close the representative-data,
parity, installed-rollback, and channel-acceptance gates.

**SeerrNG pending-request lifecycle probe (2026-10-19):** On the isolated Linux candidate from Build run [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373), SeerrNG's real `ReadarrAPI` (`b810153c`) submitted an ebook request for a synthetic work whose author was unavailable. `addBook` received a pending ID, `getPendingAuthorImport` read it, `cancelPendingAuthorImport` cancelled it, and a second poll observed the expected terminal cancellation state: `OverallStatus=Failed` with `LastError=Cancelled by user`. The cancelled record remains queryable rather than being removed. This proves request creation, polling, and cancellation across the adapter/API boundary. It used a disposable database and synthetic IDs; provider-success behavior, retries, availability, history, migrated records, production settings, full SeerrNG operator workflow, and operator acceptance remain open. The tested app artifact is code snapshot `0612cb4c`; PR commit `d9713604` added the repeatable helper and docs, and the intervening app change is Calibre selector tie-breaking rather than a SeerrNG API change. The probe is now repeatable through `integration/seerrng-request-lifecycle.mjs` and is wired after the authorization smoke in `integration/seerrng-smoke.compose.yml`. On 2026-10-19, both scripts passed when run against the isolated artifact from Build 39239940373 and SeerrNG source `b810153c`; `node --check`, Prettier, and Compose configuration validation also passed. The earlier Compose invocation used a temporary runtime image with `--no-build`. A follow-up source-build run on 2026-10-19 built the candidate through the checked-in `Dockerfile.build` from PR head `7cfb5d87` and passed both scripts after the harness prepared its disposable `/books` volume as UID/GID `99:100`. Successful fulfillment, retries, availability/history, converted records, saved operator settings, and acceptance remain open.

## Encrypted BookshelfNG portable backups

The retirement candidate adds passphrase-based `.enc` backup export and
restore using BookshelfNG's BKSENC01 format. This allows a BookshelfNG
encrypted portable backup to be selected directly in ChaptarrNG's restore
dialog, and lets ChaptarrNG create backups that BookshelfNG can read. The
passphrase is entered through the UI or sent in the
`X-Bookshelf-Backup-Passphrase` API header. ChaptarrNG's scheduled `.zip.enc`
files use a separate format and secret-file setting. Details and limits are in
the [encrypted backup guide](BOOKSHELFNG_ENCRYPTED_BACKUPS.md). This candidate
change is not in published `v0.9.977` and does not replace independent
appdata/database backups or close the release gates.

## Restore a BookshelfNG backup archive

The published `.977` restore service accepts Chaptarr/Readarr-format ZIP
archives but rejects a BookshelfNG database inside them. Merged PR #131 adds
support for a recognized BookshelfNG database in a standard
`readarr_backup_*.zip` archive: ChaptarrNG checks SQLite integrity, BookshelfNG
lineage, the source migration-version range, and the legacy table-name boundary,
then stages the database for restore. On normal startup, the existing converter
upgrades a disposable source copy, builds and validates a ChaptarrNG database,
and retains the original BookshelfNG database for rollback. The archive itself
remains untouched. The previous ChaptarrNG database is restored if conversion
fails and its temporary `.pre-restore` copy is removed after success. A
regression fixture covers restore followed by automatic conversion. ChaptarrNG
PR #131 code head `6f57d355` passed exact-head Build run
[39211491613](https://github.com/snapetech-org/chaptarrng/actions/runs/39211491613)
on Linux, macOS, Windows, PostgreSQL migration, and release-note validation.
The fixture uses the complete migration history expected in a BookshelfNG
schema-158 database. This is hosted synthetic validation, not operator-data
acceptance. The source-candidate path is not available in `.977` and must not
be used for production cutover while the retirement gates remain open.

Current `main` also accepts raw `.db` and `.xml` backup uploads through the
same restore endpoint. The `.xml` suffix is a legacy filename; the uploaded
content must still be a SQLite database. ChaptarrNG checks SQLite integrity
and database lineage before staging it, and applies BookshelfNG source-version
and table-boundary validation when the database is BookshelfNG. Normal startup
then converts the staged database and retains the source for rollback. These
raw upload extensions are not accepted by published `v0.9.977`.

The published `.977` converter does not support PostgreSQL. Current `main`
contains a separate PostgreSQL route that migrates recognized older source
schemas on a disposable PostgreSQL snapshot, and preserves the original schema
for rollback. This source candidate remains unpublished and unapproved for
operator cutover; representative populated data, independent backup restore,
installed rollback, and channel handoff are still required. See the
[PostgreSQL candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md). For SQLite,
the current source also bundles the BookshelfNG migration chain; `.977` still
requires migration 158, and databases earlier than migration 14 require that
unreleased source fix. Do not switch applications while the retirement freeze
is active.

Environment variables are outside the database conversion. If BookshelfNG
used `BOOKSHELF_ALLOW_API_KEY_QUERY=false`, ChaptarrNG honors it while
`CHAPTARR__AUTH__ALLOWAPIKEYQUERY` is unset. Prefer the ChaptarrNG setting for
new deployments; query-key authentication remains enabled by default. The
legacy `BOOKSHELF_SEERRNG_API_KEY` also remains active when
`CHAPTARR__AUTH__SEERRAPIKEY` is unset, so existing SeerrNG credentials keep
working when the container environment is carried forward. Remove the legacy
value to revoke it or set the ChaptarrNG key explicitly to rotate it. Preserve
the BookshelfNG diagnostics variables if that optional module was explicitly
enabled; ChaptarrNG honors that choice only when the archived appdata also
contains the old diagnostics module marker. See the
[HTTP security settings](HTTP_SECURITY.md),
[SeerrNG integration guide](SEERRNG_INTEGRATION.md), and
[optional diagnostics guide](OPTIONAL_DIAGNOSTICS.md).

The source candidate also reads BookshelfNG's `BOOKSHELF_OIDC_*` environment
variables when the corresponding ChaptarrNG OIDC setting is empty. This keeps
the issuer, client ID and secret, scopes, email allow-lists, allow-any-verified
choice, and private-issuer behavior when the deployment carries its existing
environment forward. A saved non-empty ChaptarrNG value takes precedence. The
legacy callback override is accepted only as an HTTPS URL ending in
`/signin-oidc`; otherwise ChaptarrNG uses its normal callback URL. Keep these
variables in the deployment definition because the appdata archive does not
include them. This source-only compatibility change is not in public `.977`,
and OIDC denies sign-in unless an allow-list or the explicit
allow-any-verified setting is configured.

### Provider configuration blockers

Provider rows are copied only when ChaptarrNG has the same implementation and
settings contract. The retirement candidate includes BookshelfNG's Hardcover
public-list and reading-status provider, so its API token, list owner, selected
lists, and settings can remain active. BookLore notification settings and
triggers also remain connected to their saved BookLore URL and account;
migration does not treat BookLore as BookOrbit. The candidate adds BookOrbit as
a separate Book Dock destination. Configure and test that separate connection
in **Settings > Connect** after an approved migration.

Provider configuration is preserved only when the implementation, contract,
and every populated saved settings field match the target provider. The
unreleased PR #131 candidate now includes compatibility ports for
BookshelfNG's OAuth-backed Goodreads bookshelf and owned-book lists and its
bookshelf/owned-book write notifications. Complete saved OAuth settings are
preserved; the preview blocks missing or partial credentials for OAuth-only
providers. A public Goodreads shelf can keep working without OAuth when it has
a valid numeric user ID. Migration reports identify the row and missing field
names without printing settings or credentials.

The candidate ports BookshelfNG's `AudiobookshelfMetadata` OPF consumer as
ChaptarrNG's distinct `LegacyAudiobookshelfMetadata` provider. The importer
maps the old provider implementation and both legacy sidecar IDs to that name,
preserving the `NullConfig` contract, metadata-file IDs, and remapped book/file
links. Unknown metadata providers or sidecar consumers appear in the preview
and block apply. ChaptarrNG's `AudioBookShelfMetadata` JSON and cover writer
remains a separate provider.

This source code does not restore Goodreads developer-key issuance or prove
that an existing token remains accepted. Goodreads stopped issuing new
developer keys and announced API retirement, so existing credentials and live
API behavior must be tested with representative accounts before cutover. The
compatibility port is not in published `v0.9.977`; do not use it for operator
data while the release freeze is active. Keep the original appdata backup and
see Goodreads' [API deprecation notice](https://www.goodreads.com/topic/show/21788520-api-deprecation).

Unknown or plugin download clients, import lists, notifications, indexers,
metadata providers, and sidecar consumers are handled the same way: preview
identifies the unsupported mapping and apply stops before promotion. A source
backup preserves the original configuration, but it does not make an
unsupported provider active in ChaptarrNG. These candidate changes are not in
published `v0.9.977` while the release freeze remains active.

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

PR #131 candidate commit `12f290bd` now also writes ChaptarrNG's persisted
`HardcoverEnabled=true` setting when migration runs with native Hardcover mode
and the source has no explicit saved mode. This keeps the target in the same
provider mode after startup conversion; an existing saved target setting is
preserved, and `HARDCOVER=false` does not enable Hardcover. The importer suite
and a disposable schema-158 startup-conversion smoke cover both cases. This is
unreleased candidate behavior and does not authorize applying the migration to
operator data while the freeze remains active.

The current ChaptarrNG work-branch candidate also maps a saved `HardcoverAuth`
database value to `HardcoverApiToken`; a saved ChaptarrNG token takes
precedence, and the original BookshelfNG database remains available in the
rollback backup. That native provider and token mapping are not in published
`v0.9.977`. The earlier integrated code snapshot `8b4c6e23` passed cross-platform
and PostgreSQL migration checks in exact-head run
[39205452341](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452341).
Representative converted-data acceptance remains open; see the [PostgreSQL
candidate notes](BOOKSHELFNG_POSTGRESQL_MIGRATION.md). Do not treat this as
supported migration behavior during the release freeze.

BookshelfNG's `BOOKSHELF_M4B_MERGE`,
`BOOKSHELF_M4B_AAC_BITRATE_KBPS`, and `BOOKSHELF_FFMPEG_PATH` variables are
also outside database conversion. The published ChaptarrNG `v0.9.977` does not
honor them. ChaptarrNG PR #129 merged the compatibility implementation to
`main` as `949ae3dd`, but it is not in `.977`; keep `BOOKSHELF_M4B_MERGE` unset
until a build with that support is available and the remaining acceptance
gates close. The current retirement candidate's
`real_m4b_tool_should_merge_and_import_legacy_multipart_audiobook_when_installed`
test passed 6/6 on candidate `12f290bd` in an isolated Linux tool image with
`m4b-tool` 0.5.2, `mp4v2` 2.1.1, and Debian Bookworm FFmpeg. It generates two
MP3 tracks and imports them through the real converter, verifying tag-based
track order, chapter titles and offsets, output metadata, library registration,
Move-mode source cleanup, and Copy-mode source retention. Failure cases verify
fallback to separate original-track imports in both modes when FFprobe cannot
read a source and when a controlled FFmpeg error makes the real `m4b-tool`
process fail. The Linux build workflow runs these six cases in the checked-in
`integration/Dockerfile.m4b-test` image. Synthetic embedded-chapter Move/Copy and TagLib-write cases pass on c4fa; representative M4B files and restart/resume remain open.
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
channel acceptance status. The ChaptarrNG shell helper can also queue an
all-format retry using RetryUnmappedMatch and unmappedFiles.scope=all; see the
[unmapped-files guide](UNMAPPED_FILES.md). It is not in public `.977`.

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

## New author format paths

The current migration candidate preserves BookshelfNG's
`AutoInitializeFormatSpecificAuthorPaths` setting; it remains opt-in and is
false when the source did not save it. When enabled, saved
`NewAuthorEbookRootFolderPath` and `NewAuthorAudiobookRootFolderPath` values map
to ChaptarrNG's `DefaultEbookRootFolderPath` and
`DefaultAudiobookRootFolderPath`. Existing non-empty ChaptarrNG defaults take
precedence, and the original BookshelfNG config rows remain available for
review. When BookshelfNG had the feature disabled, its new-author root overrides
are not applied. The setting only affects newly added authors; it does not
backfill existing paths or move files. This source change remains under the
release freeze and still needs hosted validation and migration acceptance.

## Extra files after rename

The unpublished migration candidate preserves BookshelfNG's
`MoveExtraFilesOnRename` setting in the converted database, exposes it in
Media Management, and includes it in settings backups. It remains off by
default. When enabled, ChaptarrNG moves only extensions listed in
`ExtraFileExtensions` from a renamed file's old folder after all renamed files
from that folder agree on one destination. It leaves the files in place if the
source still contains a recognized book file or a subfolder, or if the
destination already contains a file with the same name. This behavior is not
in the published `.977` release and has not passed operator-data or installed
rollback acceptance; keep the release freeze in place.

## Read-only preview and gated apply path

Stop BookshelfNG first and make an independent copy of the entire data
directory. Run the preview with the **same regular ChaptarrNG release** that
will run the migrated service. Preview performs the full conversion into a
temporary staged database, prints the row disposition for every source table,
and leaves the source database untouched.

The commands below are for an approved migration-capable release. The published
`.977` image does not include the current provider-identity migration; keep the
release freeze above in effect until an approved image is published. Set
`CHAPTARRNG_IMAGE` to that exact versioned image or immutable digest.

```sh
CHAPTARRNG_IMAGE="ghcr.io/snapetech-org/chaptarrng:<approved-version-or-digest>"
docker run --rm \
  -e HARDCOVER=true \
  -e HARDCOVER_NATIVE=true \
  -v /host/path/to/bookshelfng-config:/config \
  "$CHAPTARRNG_IMAGE" \
  dotnet /app/Chaptarr.dll -preview-bookshelf-migration -data=/config
```

These values are shown for a native Hardcover source. Use `HARDCOVER=false`
for a Goodreads-primary source, and preserve the source's `HARDCOVER_NATIVE`
value if it was explicitly set. Preview, apply, and automatic startup must use
the same provider-mode values.

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

The current ChaptarrNG migration candidate maps legacy
`MetadataFiles.Consumer=Readarr` rows to `LegacyAudiobookshelfMetadata`. This
keeps those sidecar records attached to ChaptarrNG's retained legacy metadata
consumer; unknown consumer values still block apply. The public `.977` release
does not include this mapping.

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
CHAPTARRNG_IMAGE="ghcr.io/snapetech-org/chaptarrng:<approved-version-or-digest>"
docker run --rm \
  -e HARDCOVER=true \
  -e HARDCOVER_NATIVE=true \
  -v /host/path/to/bookshelfng-config:/config \
  "$CHAPTARRNG_IMAGE" \
  dotnet /app/Chaptarr.dll -migrate-bookshelf -confirm-bookshelf-service-stopped -data=/config
```

Apply retains a backup beside the original database. Keep the independent
directory copy and generated database backup until you have checked the
library, root paths, profiles, indexers, download clients, notifications, and
SeerrNG connection. The explicit stopped-service flag makes the required
quiescent data directory an operator-confirmed precondition.

### ChaptarrNG-owned Docker handoff helper

Run the host helper from a ChaptarrNG source checkout. It is a wrapper around
the same application migration and restore commands documented above; it does
not perform a second conversion implementation. Use a separate working copy
for preview while the retirement gate remains open. After operator approval,
the migration sequence is:

```sh
scripts/bookshelfng-to-chaptarrng.sh snapshot \
  --data-dir /host/path/to/bookshelfng-config \
  --backup-dir /host/path/to/external-backups

scripts/bookshelfng-to-chaptarrng.sh preview \
  --data-dir /host/path/to/working-copy \
  --backup-dir /host/path/to/external-backups \
  --image "$CHAPTARRNG_IMAGE" \
  --provider-mode native-hardcover
```

Set `CHAPTARRNG_IMAGE` to the exact versioned image or immutable digest that
will run the replacement. Use `--provider-mode goodreads` for a
Goodreads-primary source. Preview runs in a temporary container and does not
apply the conversion. Review its report before proceeding.

After stopping every process using the data directory, and only after the
retirement gates close, apply with the same image and provider mode plus
`--confirm-stopped`. The helper saves the complete appdata archive outside the
source directory before running ChaptarrNG. For rollback, use the filename of
the retained `readarr.db.bookshelfng-backup-*` database directly inside
`--data-dir`; `rollback-preview` remains separate from `rollback`, and actual
restore also requires `--confirm-stopped`. Keep the independent full-directory
archive until the restored BookshelfNG instance has been checked.

### Windows standalone service migration

The existing BookshelfNG WPF installer still installs and upgrades BookshelfNG.
ChaptarrNG has a candidate-only WPF migration installer at
`distribution/windows/installer-net`; it is built for internal Windows CI and
has not been published. The installer supports one SQLite database and the
standard `NT AUTHORITY\LocalService` service account. It requires a provider
identity choice, makes and verifies a full rollback backup, previews before
apply, and checks the migrated database and ChaptarrNG's local health. A
read-only SQLite `PRAGMA data_version` guard remains active through preview
review and blocks committed changes from another connection immediately before
apply. WAL representation changes are diagnostic and do not count as committed
database changes.

The service handoff keeps the existing `Readarr` registration until ChaptarrNG
has passed its health check. After the verified backup, the installer records
Readarr's original startup mode and whether it was running, then disables it
so it cannot start beside ChaptarrNG. A helper waits for the installer to
close, confirms both services target the same data directory and ChaptarrNG is
running, then removes the disabled Readarr registration. If cleanup cannot be
scheduled, Readarr stays disabled and the installer reports that state.

Automatic rollback stops ChaptarrNG, restores the verified files and database,
and reuses the existing Readarr registration when it is still present. It
restores Readarr's startup mode and starts it only if it was running before the
migration. If post-exit cleanup already removed the service, rollback recreates
it with the saved startup mode and supported LocalService identity. The
installer's **Restore BookshelfNG** action remains available while the retained
backup exists.

This is still source-only migration tooling. The published `v0.9.977` Windows
binary does not preserve `-data` when registering its service, and the manual
service-registration commands from earlier drafts are not a supported
handoff. Do not change the service or apply the migration during the release
freeze. PostgreSQL, custom service accounts, multiple databases, unsupported
schemas, and representative operator-data rollback are not covered by this
installer. Keep BookshelfNG available until the installer and all retirement
acceptance gates have passed.

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
source. The source compatibility image targets select the matching mode for the
legacy mutable aliases and use port `8787` with a fresh data directory. They
have not been published or enabled while the release freeze is active; a saved
`config.xml` port or explicit `Chaptarr__Server__Port` setting takes precedence.
The ordinary ChaptarrNG image still defaults to `8789` for fresh installs. For
an installation that used the default BookshelfNG port, the shape is:

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

The saved BookshelfNG `config.xml` keeps the internal port at `8787`. If the old
instance used a different port, preserve that port in both its container
configuration and host mapping.

## Unpublished candidate populated SQLite round trip (2026-10-19)

The unpublished ChaptarrNG PR #131 candidate (build version `0.9.978.0`)
completed a full preview/apply/restore rehearsal on a task-scoped copy of a
private populated BookshelfNG migration-158 SQLite database. The live
BookshelfNG service remained running, the source snapshot stayed byte-identical,
and every candidate command used the separate copy with `HARDCOVER=true`. This
validates the migration utility path, not an installed service handoff.

- Migration preview passed without changing the source database.
- The explicit apply mode and its `-confirm-bookshelf-service-stopped` option
  were used only against the isolated copy. The live service stayed running,
  so this did not validate the actual stop-before-apply handoff. The converted
  database reached ChaptarrNG schema 121 and passed SQLite integrity and
  foreign-key checks; the original schema-158 backup remained retained.
- Rollback preview passed without changing either the converted database or
  the source backup.
- Explicit rollback restored a byte-identical copy of the retained schema-158
  source backup and preserved the converted schema-121 database as a separate
  ChaptarrNG backup.
- A fresh copy of the restored database opened in the BookshelfNG `hardcover`
  image on Docker's isolated `none` network; the configured `/ping` endpoint
  returned HTTP 200. This verifies startup and database reopen only, not the
  complete library workflow or an installed image switch.
- The `.978` Linux app artifact from Build
  [39267876585](https://github.com/snapetech-org/chaptarrng/actions/runs/39267876585)
  also returned HTTP 200 from `/ping` against a fresh converted schema-121 copy,
  running on the published `.977` runtime image with empty scratch mounts for
  configured roots and an internal-only network. This checks candidate app
  startup, not candidate image packaging or a live image switch.

The private library contents, counts, identifiers, and hashes are omitted.
This validates one populated SQLite migration and restore path. It does not
classify ambiguous formats, prove every data mapping or client behavior, or
close older-schema, PostgreSQL, full feature-parity, or installation-channel
acceptance. Configured provider rows in this snapshot passed compatibility
checks without blockers, but no live provider calls were tested. With native
Hardcover mode enabled, every migrated author, book, and edition retained its
Hardcover identity; none were assigned Goodreads IDs. Keep the release freeze
and production cutover restrictions in place; the candidate is not a published
migration tool.

## Exact-head artifact migration and SeerrNG smoke (2026-10-20)

The Linux artifact from ChaptarrNG Build
[39394240337](https://github.com/snapetech-org/chaptarrng/actions/runs/39394240337)
was run from the current application snapshot
`29852b1334ee049e30de84b44806932a3e9364f4`. A task-scoped config and library
were seeded from the checked-in BookshelfNG schema-158 fixture. Normal startup
converted the database to ChaptarrNG schema 121 and retained a schema-158
BookshelfNG backup. Both databases passed `PRAGMA integrity_check`; the app
reported `0.9.978.0`.

SeerrNG at source commit
[`ce632a6e6834fe094eb24ceda9b8a388472cc23e`](https://github.com/snapetech-org/seerrng/commit/ce632a6e6834fe094eb24ceda9b8a388472cc23e)
read the migrated audiobook and ebook through its real `ReadarrAPI` adapter
and verified their Hardcover identities. The restricted key read capabilities
and both format libraries, was denied administrative and unrelated command
routes, and the global key retained administrative access. ChaptarrNG's
`integration/seerrng-request-lifecycle.mjs` then created, polled, and cancelled
a synthetic pending import, which remained queryable in its terminal state.

This uses only synthetic records. The `.m4b` and `.epub` files are placeholders,
so it does not test media decoding, file access, provider fulfillment, operator
data, PostgreSQL operator data, or installed rollback. The Build's Windows
installer test is tracked separately in the exact-head validation section.
Build [39394240337](https://github.com/snapetech-org/chaptarrng/actions/runs/39394240337)
passed Linux, macOS, Windows, PostgreSQL migrations, and native-runner receipt
reconciliation. The Windows job passed the installer fingerprint, service
upgrade/rollback, cleanup, and selected-data-directory checks. No release or
compatibility bridge was published.

## Current candidate SeerrNG source-build smoke (2026-10-20)

ChaptarrNG source snapshot `ef0a144da7a54f98cd5e13347a0ed06a89c15cfc` built
through the checked-in `Dockerfile.build` with its pinned .NET 10.0.13 runtime.
The local SeerrNG source at
[`ce632a6e6834fe094eb24ceda9b8a388472cc23e`](https://github.com/snapetech-org/seerrng/commit/ce632a6e6834fe094eb24ceda9b8a388472cc23e)
read the synthetic audiobook and ebook converted from the BookshelfNG
migration-158 fixture. ChaptarrNG reached schema 121, retained its schema-158
backup, and both databases passed SQLite integrity and foreign-key checks.
SeerrNG's restricted key read capabilities and both formats while
administrative and unrelated command routes were denied. The real `ReadarrAPI`
created, polled, and cancelled a synthetic pending import.

This run used placeholder `.m4b` and `.epub` files; it does not validate media
processing, successful provider fulfillment, operator data, PostgreSQL
operator data, or installed rollback. The later source commit
`ad40d565` additionally requires the audiobook media scope and
non-overriding narrator-preference payload for the restricted key. Focused
ChaptarrNG NUnit tests cover both formats, allowed and denied narrator calls,
and plus-encoded spaces; SeerrNG's focused adapter tests confirm that it sends
`mediaType=audiobook`. This migration smoke predates the later narrator-scope
and UI-shell changes. At that time, exact-head Build
[39474807076](https://github.com/snapetech-org/chaptarrng/actions/runs/39474807076)
passed Linux, Windows, and PostgreSQL migration jobs for source commit
`55f6ae1a`; the macOS job was cancelled before runner setup on both attempts.
The current exact-head result is recorded at the top of this guide. No release
or compatibility bridge was published.

## Populated SQLite migration and restore rehearsal (2026-10-21)

The ChaptarrNG `main` candidate at
[`69abed74`](https://github.com/snapetech-org/chaptarrng/commit/69abed74de74250c5ad3f6bed2c0c762dbcd813f)
passed preview, explicit migration, restore preview, and rollback against
isolated SQLite snapshots from two populated BookshelfNG installations. Both
sources were at migration 158 and used Hardcover mode. Each preview covered 39
source tables with no blocking issues; the import report accounted for every
source row, including the command, provider-health, scheduled-task, and
migration-history state that ChaptarrNG intentionally recreates.

Each explicit apply reached ChaptarrNG schema 121, passed SQLite integrity and
foreign-key checks, and retained a BookshelfNG source backup. The saved
Hardcover token was present under `HardcoverApiToken` with the expected
`Bearer ` prefix normalization, and `HardcoverEnabled` remained true. Restore
preview and apply passed for both copies. The restored database and retained
backup matched the original snapshots by table definitions and all table row
contents; integrity checks passed and no foreign-key violations were present.
The original snapshots remained byte-unchanged. The live BookshelfNG services
were left running and continued to answer `/ping`.

This establishes populated SQLite conversion and database rollback for these
two data sets on the current source candidate. Normal startup on fresh copies
of the same snapshots is recorded below. Filesystem or media processing, live
provider behavior, SeerrNG workflows, PostgreSQL operator data, and installed
package, image, Helm, or Unraid handoffs remain unvalidated. The public
`v0.9.977` release was not the tested candidate; publication and compatibility
bridges remain frozen.

## Populated SQLite normal startup and configuration preservation (2026-10-21)

A Linux x64 candidate built from ChaptarrNG `main` at
[`69abed74`](https://github.com/snapetech-org/chaptarrng/commit/69abed74de74250c5ad3f6bed2c0c762dbcd813f)
was started against fresh, isolated copies of both populated source snapshots.
Each container had no network access, mounted only its copied `/config`, and
used the source's `HARDCOVER=true` setting. ChaptarrNG automatically converted
each BookshelfNG database from migration 158 to schema 121 during normal
startup.

The audiobook copy returned `/ping` HTTP 200 on its saved port 8788 after 102
seconds. The ebook copy returned HTTP 200 on its saved port 8787 after 105
seconds. Both converted databases passed SQLite integrity checks and had zero
foreign-key violations. Their original `config.xml` files retained
`AuthenticationMethod=Forms`, `AuthenticationRequired=Enabled`, and the same
API key values after startup; no credential value is recorded here. A request
to `/api/v1/system/status` without an API key returned HTTP 401 on the
converted audiobook copy. The original source snapshots stayed byte-unchanged,
and both live BookshelfNG services remained up and answered `/ping` with HTTP
200.

This verifies normal startup and configuration preservation for these two
isolated SQLite snapshots. The configured library directories were not mounted
and the container network was disabled, so this does not validate media file
access, provider calls, SeerrNG workflows, an image or package switch, rollback
on an installed service, or full BookshelfNG feature parity.

## StartOS `/init` and populated-database rehearsal (2026-10-21)

A local Linux/amd64 `bookshelf-hardcover` image built from ChaptarrNG commit
[`2d6124e2`](https://github.com/snapetech-org/chaptarrng/commit/2d6124e2) was
run through a wrapper with `ENTRYPOINT ["/init"]` and `CMD []`, matching the
StartOS command that invokes `/init` without arguments. The isolated
containers had no network and mounted task-scoped copies at `/config`,
`/books`, and `/downloads`. Two copied populated BookshelfNG SQLite databases
converted from schema 158 to ChaptarrNG schema 121 during normal startup.

The no-argument run preserved the source config's port `8787`, Forms
authentication, and API key. `/ping` returned HTTP 200, the Docker
health-check command passed, SQLite integrity was `ok`, and there were zero
foreign-key violations. An unauthenticated `/api/v1/system/status` request
returned HTTP 401. The original source database snapshot remained unchanged.
Built image metadata exposed only `8787/tcp` for both compatibility targets;
a standard image build exposed only `8789/tcp`.

This exercises the Linux image and copied data only. The `/books` and
`/downloads` mounts were empty, the container network was disabled, and the
StartOS package was not built or installed on a device. It does not validate
media access, provider calls, package update, StartOS backup/restore, or
rollback on an installed StartOS service.

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

The parked StartOS package is still BookshelfNG package ID `bookshelfng` and
pins its `.37` native-Hardcover image. Its wrapper expects port `8787` and
starts `/init`; public ChaptarrNG `v0.9.977` uses `/entrypoint.sh`, defaults
to port `8789`, and does not convert native Hardcover IDs. The current
production-image candidate adds an `/init` launcher and preserves the legacy
port/provider defaults in its compatibility target. A disposable wrapper
image based on the public `.977` digest passed startup through `/init`, `/ping`
on `8787`, and checks for the `/config`, `/books`, and `/downloads` mounts.
This only verifies the launcher. The StartOS package has not been updated or
tested on a StartOS device. Keep the package on BookshelfNG until the released
compatibility image passes package build, upgrade, backup, and rollback checks
on a StartOS device.
See the [channel status](../packaging/README.md#package-identities).

### Helm chart handoff

ChaptarrNG's [`charts/bookshelfng`](../charts/bookshelfng/README.md) is a
source-only, fail-closed compatibility chart in ChaptarrNG `main`. It keeps the
legacy chart and Kubernetes resource identities, including the release name,
StatefulSet selector, Service, port `8787`, `/config` mount, and default PVC
name. It requires explicit migration confirmation, the source's
Hardcover/Softcover mode, and an immutable ChaptarrNG image digest. The release
workflow now contains a versioned chart publisher for the existing OCI chart
identity, but both release-readiness gates, the migration bridge gate, and an
explicit bridge request are required; the publisher has not run and no chart
artifact is available during the freeze.

An installed PVC upgrade, GitOps sync, and chart rollback have not been tested,
so this does not authorize a live cutover. After all migration and channel
acceptance gates close, Helm users can upgrade from the same
`oci://ghcr.io/snapetech/bookshelfng/bookshelfng` source while keeping the
existing release name, namespace, values, and PVC. Pinned chart archives need a
manual update. See the chart guide for the command shape; do not use it against
live BookshelfNG data during the release freeze.

BookshelfNG's in-app updater still uses its existing upstream update service;
Snapetech cannot redirect that service. The practical handoff paths are a
manual image/package/chart-version change or a separately validated
legacy-identity bridge.
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
