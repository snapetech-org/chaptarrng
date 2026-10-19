# SeerrNG integration

ChaptarrNG exposes its Bookshelf API to SeerrNG using the Readarr-compatible
API shape. The integration capability contract is available at
`GET /api/v1/system/capabilities`; SeerrNG uses it to select the provider-ID
dialect, format-scoped routes, paged library endpoint, edition identity rules,
pending author-import support, restricted-key support, and format-specific
availability statistics. It also advertises `editionCatalog` for clients that
show a person the known editions and audiobook performances available for a
book.

For a broader overview of maintained behavior beyond the shared Chaptarr base,
see [ChaptarrNG fork changes and upstream replacement criteria](./FORK_FEATURES.md).
Chaptarr's current development branch has its own pending-import subsystem;
this guide focuses on the format, identity, capability, and service-key
contract SeerrNG uses.

## Edition catalog for edition cards

Clients can request `GET /api/v1/book/{id}/editions/catalog` to list the known
editions for one book. The response includes provider identity, title,
language and format, narrator names, publisher, release date, duration, cover,
monitoring state, and whether a local file is owned. `production` and
`abridged` use `unknown` unless the edition metadata gives clear evidence;
multiple narrator names alone do not imply a full-cast production. Facade
requests receive the edition ID for the selected provider dialect.

**Availability:** current ChaptarrNG source includes the edition catalog,
series narrator matrix, and wanted-edition variant capability. The migration
candidate also returns provider IDs in the selected facade dialect, proxies
remote cover URLs, and derives ownership from the media-file service. Public
ChaptarrNG `v0.9.977` predates these endpoints. PR #131 remains a draft, and
release publication stays frozen until the BookshelfNG retirement gates pass.

The PR #131 candidate also adds an **Editions and Narrations** tab to the book
details page. It lists known editions with narrators, production, duration,
abridgement, language, publisher, and selected/file-owned status. Metadata
that does not state a value is shown as not provided rather than inferred.
The tab and its catalog route are source-only and are not available in public
`v0.9.977`.
Earlier integrated candidate code snapshot `8b4c6e23` incorporates current ChaptarrNG
`main`, including the Editions tab and series narrator comparison, and retains
explicit labels for unknown and not-applicable edition metadata. Local
translation, type, targeted ESLint, and Stylelint checks pass. Exact-head Build
run [39205452341](https://github.com/snapetech-org/chaptarrng/actions/runs/39205452341)
passed Linux, macOS, Windows, PostgreSQL migration, release-note, and
runner-receipt checks; CodeQL, dependency review, security scan, and NuGet
submission also passed. The UI is source-only in public `v0.9.977`, and the
candidate remains draft while operator-data, full SeerrNG workflow, rollback,
and channel acceptance remain open.

**Prior PR #131 application-code validation (2026-10-19):** snapshot `0612cb4c90b75818e5349eb868cc0638825de02a` passed [Build 39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373): Linux, macOS, and Windows tests, PostgreSQL migration smoke, native-runner receipt reconciliation, and the release-note contract. [CodeQL 39239934583](https://github.com/snapetech-org/chaptarrng/actions/runs/39239934583), [security 39239940344](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940344), [NuGet 39239932165](https://github.com/snapetech-org/chaptarrng/actions/runs/39239932165), and [dependency review 39239940313](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940313) passed. Disposable-fork validation [39239940512](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940512) was skipped by its external-fork gate. The SeerrNG adapter and request-route unit suite at source commit `b810153c` passed 4 files / 283 tests. The built Linux artifact also passed the earlier empty-library API and service-key smoke. The populated source-build check for later code is recorded below.

**SeerrNG pending-request lifecycle probe (2026-10-19):** On the isolated Linux candidate from Build run [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373), SeerrNG's real `ReadarrAPI` (`b810153c`) submitted an ebook request for a synthetic work whose author was unavailable. `addBook` received a pending ID, `getPendingAuthorImport` read it, `cancelPendingAuthorImport` cancelled it, and a second poll observed the expected terminal cancellation state: `OverallStatus=Failed` with `LastError=Cancelled by user`. The cancelled record remains queryable rather than being removed. This proves request creation, polling, and cancellation across the adapter/API boundary. It used a disposable database and synthetic IDs; provider-success behavior, retries, availability, history, migrated records, production settings, full SeerrNG operator workflow, and operator acceptance remain open. The tested app artifact is code snapshot `0612cb4c`; PR commit `d9713604` added the repeatable helper and docs, and the intervening app change is Calibre selector tie-breaking rather than a SeerrNG API change. The probe is now repeatable through `integration/seerrng-request-lifecycle.mjs` and is wired after the authorization smoke in `integration/seerrng-smoke.compose.yml`. On 2026-10-19, both scripts passed against the isolated artifact from Build 39239940373 and SeerrNG source `b810153c`; `node --check`, Prettier, and Compose configuration validation also passed. The earlier Compose invocation used a temporary runtime image with `--no-build`. A follow-up source-build run on 2026-10-19 built the candidate through the checked-in `Dockerfile.build` from PR head `7cfb5d87` and passed both scripts after the harness prepared its disposable `/books` volume as UID/GID `99:100`. Successful fulfillment, retries, availability/history, converted records, saved operator settings, and acceptance remain open.

## Configure a dedicated service key

ChaptarrNG `0.9.941` and later can use a dedicated credential for SeerrNG.
Configure `CHAPTARR__AUTH__SEERRAPIKEY` in the ChaptarrNG container or secret
manager, then use its value as the API key in SeerrNG under **Settings →
Services → Bookshelf**. Keep the regular ChaptarrNG API key separate; it keeps
its full administrative access.

The SeerrNG credential can inspect book libraries, profiles, queue and history,
look up books and authors, add books, update an individual book, run a single
book search, read pending author imports, and cancel a pending author import.
It can access the native `/api/v1` routes and the advertised
`/readarr/{dialect}/{mediaType}/api/v1` compatibility routes. Media covers are
available so the SeerrNG UI can display book artwork.

The credential cannot change global settings, delete books or authors, delete
files, run other commands, or use author media-move operations. Those routes
remain available only through the regular API key or an administrator session.
SeerrNG's standard request, search, status, and pending-import workflows use
the restricted route set.

For older ChaptarrNG releases, use the regular API key. The separate service
key is an additive option and does not change existing authentication.

## Verify a local integration build

The SeerrNG repository includes `scripts/chaptarrng-integration-smoke.mjs`.
Build its server first with `pnpm build:server`, then run the integration
Compose file from the ChaptarrNG repository. It uses named disposable volumes;
the fixture seeds a BookshelfNG migration-158 SQLite database before
ChaptarrNG starts, so normal startup performs the conversion. Set the absolute
SeerrNG checkout and `node_modules` paths, the Six Labors license-file path,
and distinct random values for `CHAPTARR_GLOBAL_API_KEY` and
`CHAPTARR_SEERR_API_KEY`:

```sh
export SEERRNG_SOURCE=/path/to/seerrng
export SEERRNG_NODE_MODULES=/path/to/seerrng/node_modules
export SIXLABORS_LICENSE_FILE=/path/to/sixlabors.lic
export CHAPTARR_GLOBAL_API_KEY='use-a-separate-long-random-key'
export CHAPTARR_SEERR_API_KEY='use-another-long-random-key'

docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml down -v
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml up --build -d chaptarrng
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml run --rm --no-deps smoke
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml down -v
```

The fixture refuses non-empty config and library volumes. It contains one
author, one mixed-format book, audiobook and ebook editions, and tiny synthetic
`.m4b`/`.epub` placeholder files; they are not valid media and do not exercise
M4B decoding or merge behavior. A separate real-tool importer check passed six
synthetic multipart M4B cases; representative M4B media and restart/resume remain unverified; synthetic embedded-chapter preservation is covered on c4fa. The smoke checks that SeerrNG reads both
converted formats and their Hardcover identities, confirms status and
capability discovery, rejects the service key for administrative and
unrelated command routes, and runs the compiled SeerrNG adapter through an
ebook add, pending-import poll, and cancellation. `SEERRNG_NODE_MODULES` must
point to the installed dependency directory so the separate smoke container
can resolve SeerrNG's runtime packages. Use `down -v` afterward to delete the
named test volumes; do not point this harness at operator appdata.

## Recorded smoke evidence

On 2026-10-19, `scripts/chaptarrng-integration-smoke.mjs` passed against the Linux artifact from Build run [39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373), reporting ChaptarrNG `0.9.978.0`. It discovered the SeerrNG capability contract and read the empty paged library using the restricted key; `/config/host` and an unrelated `/command` were denied, and the global key was accepted by `/config/host`. The smoke used a disposable SQLite database and synthetic keys. It establishes the empty-library API and authorization contract only. The separate lifecycle probe below covers one unavailable-author add/poll/cancel path; availability statistics, history, successful provider lookup, converted records, unchanged production settings, and a real BookshelfNG library remain unvalidated.

**Current source-built populated conversion smoke (2026-10-19):** ChaptarrNG
candidate commit `12f290bd` built through the checked-in `Dockerfile.build`
and started with a disposable BookshelfNG migration-158 SQLite fixture. The
fixture was seeded in named volumes, and normal startup converted it to schema
121 while retaining the schema-158 source backup. With `HARDCOVER=true`, the
candidate persisted `HardcoverEnabled=true`. SeerrNG's real `ReadarrAPI` at
commit `fdc439aa9` read both the migrated audiobook and ebook and verified
their provider identities. The restricted service key boundary and the
pending add/poll/cancel lifecycle also passed. The audiobook and ebook files
were synthetic placeholders, not valid media. Importer tests passed 43/43;
Compose configuration, `node --check`, and Prettier checks passed. This local
source-build smoke closes the earlier empty-library-only check for this
synthetic conversion path. It does not verify operator records, older source
schemas, PostgreSQL operator data, live provider behavior, representative chaptered M4B media, successful request fulfillment, installed rollback,
or deployment channels.
No release or bridge was published.

On 2026-10-18, `scripts/chaptarrng-integration-smoke.mjs` passed against
ChaptarrNG candidate `0.9.978.0` from PR #131 source head
`1a0a1cf78b404fe86d7b266facd8e843e435d9b4` (Build run
[39169786430](https://github.com/snapetech-org/chaptarrng/actions/runs/39169786430))
and SeerrNG source commit `0b267d526`. The candidate started with a populated
synthetic migration-158 fixture created by the published BookshelfNG `.71`
binary. The smoke verified status and capability discovery, one audiobook
through SeerrNG's format-scoped paged-library adapter, denial of administrative
and unrelated command routes using the restricted key, and administrator
access using the global key. The raw ChaptarrNG API exposed one author and
separate audiobook and ebook records with their migrated provider IDs.

This verifies the populated read path and key boundary. It does not exercise
end-to-end SeerrNG request creation, availability statistics, history,
pending-import retry/cancel operations, unchanged production settings, or a
real BookshelfNG library.

On 2026-10-18, the same SeerrNG harness also passed against the Linux artifact
from exact PR #131 head
[`0434148898576e1b220392163f18096682385400`](https://github.com/snapetech-org/chaptarrng/commit/0434148898576e1b220392163f18096682385400),
Actions run [39176823002](https://github.com/snapetech-org/chaptarrng/actions/runs/39176823002),
with SeerrNG source commit `0b267d5261524a2b0c233f59da8327180045c39e`. The
ChaptarrNG artifact reported version `0.9.978.0`. The run used a disposable,
empty SQLite database and synthetic SeerrNG runtime configuration. SeerrNG's
adapter read status, discovered the capability contract, and received the
empty paged book library. The restricted key was denied `/config/host` and
unrelated `/command`; the global key was accepted by `/config/host`.

That earlier-head smoke validates API and key behavior with an empty library;
it does not replace the earlier populated-fixture smoke above. Neither run
exercises end-to-end SeerrNG request creation, availability statistics,
history, pending-import retry/cancel operations, unchanged production
settings, or a real BookshelfNG library. The earlier populated fixture used
candidate head `1a0a1cf78b404fe86d7b266facd8e843e435d9b4`; the current-head
smoke does not establish converted-data acceptance.

On 2026-10-18, the local Compose smoke passed against the earlier PR #131
source head `ddec3e8104f02fe15e1885c617cdb621e2f71fc5`, whose application-code
snapshot is `2c0401fc`, and SeerrNG source commit `10f06eab9`. It built the
candidate from source and used a disposable config volume. SeerrNG discovered
the capability contract and read the empty paged library with its restricted
key; administrative and unrelated command routes were denied. This confirms
the earlier source candidate's API and key boundary. The empty-library run
does not establish populated migration acceptance or end-to-end SeerrNG
request, history, availability, or pending-import behavior.

## Narration and edition choices

These read endpoints and the existing wanted-edition endpoint let SeerrNG show
narration choices before a request. A client should read `features` from
`GET /api/v1/system/capabilities` first and hide a control when its flag is
absent, so older ChaptarrNG versions keep working.

| Capability flag | Endpoint | Purpose |
| --- | --- | --- |
| `editionCatalog` | `GET /api/v1/book/{id}/editions/catalog` | Every known edition with narrators, production, abridgement, length, language, publisher, release date, cover, whether it is the monitored selection, and whether a file exists |
| `seriesNarratorMatrix` | `GET /api/v1/series/narrators/matrix?title=…&authorId=…` | For each narrator: the volumes they cover, the volumes missing, and the volumes already owned; for each volume: the narrators known across all editions |
| `wantedEditionVariants` | `POST /api/v1/book/{id}/editions/wanted` with `asNewVariant: true` | Track the chosen narration alongside the current selection instead of replacing it |

Fields the metadata does not state are reported as `unknown` (`production`,
`abridged`) or left empty; they are never inferred from a file name. A narrator
is `full-cast` when the edition is flagged GraphicAudio or its own text says
full cast or dramatized, `solo` for exactly one narrator, and otherwise
`unknown`.

`PUT /api/v1/series/{seriesId}/narrators/preferred` (already available) makes one
narrator the series preference; SeerrNG uses it for **Collect With**.
