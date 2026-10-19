# Integration smoke tests

The SeerrNG smoke stack builds the ChaptarrNG candidate, seeds a disposable
BookshelfNG migration-158 SQLite fixture, and starts ChaptarrNG on an isolated
Compose network. Normal startup converts the source fixture. The smoke then
checks both migrated formats through SeerrNG, capability discovery, the
restricted service key, and a real SeerrNG adapter add/poll/cancel cycle for
an unavailable synthetic author.

Set the inputs in the shell; the Compose file does not need a `.env` file:
the SeerrNG checkout must have dependencies installed and its server built
first (`pnpm install --frozen-lockfile && pnpm build:server` from that checkout).
The smoke container mounts both the source tree and its installed `node_modules`
read-only so it can run SeerrNG's integration script with the same dependencies.
It also mounts this integration directory read-only; its second script imports
the compiled SeerrNG `ReadarrAPI` adapter from `/seerrng/dist` and sends an
isolated pending-request lifecycle through ChaptarrNG's HTTP API. The ChaptarrNG
container receives named config and library volumes. A Python initializer
refuses non-empty volumes, creates a BookshelfNG schema-158 database from the
checked-in shared test fixture, creates tiny synthetic `.m4b`/`.epub`
placeholders, and sets the volume owner to UID/GID `99:100`. The placeholders
are not valid media and do not exercise M4B decoding or merging. A separate
tool-enabled importer test validates synthetic multipart M4B creation and
fallback; it does not use representative operator files.

## Real-tool M4B importer check

The Linux build workflow builds `integration/Dockerfile.m4b-test` and runs
`real_m4b_tool_should_merge_and_import_legacy_multipart_audiobook_when_installed`
against the built test assembly. The image pins `m4b-tool` 0.5.2 and MP4v2
2.1.1 and supplies FFmpeg, PHP, and the MP4v2 utilities. The test generates
two synthetic MP3 tracks and covers merge ordering, chapter titles and offsets,
metadata, registration, Move/Copy source handling, and fallback when a source
is unreadable or FFmpeg fails. It also merges two synthetic M4B inputs carrying
embedded chapters in both Move and Copy modes. Successful merges rewrite the
generated M4B title with TagLibSharp and verify that chapter titles and offsets
survive the tag write. The original six MP3 cases passed locally on 2026-10-19.
The embedded-chapter Move/Copy cases and TagLib write/reprobe check passed in
exact-head Linux Build [39288736177](https://github.com/snapetech-org/chaptarrng/actions/runs/39288736177)
on candidate commit `c4fa8125`; that run also passed the full Linux suite.
Windows build and test steps passed, while macOS was cancelled before runner
acquisition. Representative audiobooks and restart/resume remain open. The
later exact-head Build [39292223717](https://github.com/snapetech-org/chaptarrng/actions/runs/39292223717)
on `b7e900d4` passed all three platform suites and the Linux pinned-tool M4B
smoke, confirming these synthetic checks on the current candidate.

After building the test assembly as in `.github/workflows/build.yml`, run it
locally with:

```sh
docker build -f integration/Dockerfile.m4b-test \
  -t chaptarrng-m4b-test:local integration
docker run --rm --network none \
  --volume "$PWD:/workspace" \
  --workdir /workspace/_tests/net10.0 \
  chaptarrng-m4b-test:local \
  dotnet vstest Chaptarr.Core.Test.dll \
    --TestCaseFilter:"FullyQualifiedName~real_m4b_tool_should_"
```

This validates synthetic conversion, input and output chapter handling, and
recovery behavior only. It does not close representative M4B or restart/resume
acceptance.

```sh
export SIXLABORS_LICENSE_FILE="$HOME/Downloads/sixlabors.lic"
export SEERRNG_SOURCE="/path/to/seerrng"
export SEERRNG_NODE_MODULES="$(realpath "$SEERRNG_SOURCE/node_modules")"
export CHAPTARR_GLOBAL_API_KEY="$(openssl rand -hex 24)"
export CHAPTARR_SEERR_API_KEY="$(openssl rand -hex 24)"

docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml \
  down -v --remove-orphans
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml up --build -d chaptarrng
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml run --rm --no-deps smoke
docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml down -v --remove-orphans
```

Use distinct test API keys and a valid Six Labors build license. Keep the
license outside the checkout; `.dockerignore` excludes it from the build
context, and Compose mounts it only as a BuildKit secret.

The lifecycle probe uses synthetic provider IDs that are expected to be
unavailable. It verifies that the adapter receives a pending ID, can poll it,
and can cancel it; cancellation remains queryable as a terminal `Failed` state
with `LastError` set to `Cancelled by user`. The ChaptarrNG configuration and
book volumes are disposable; remove them with the `down -v` command above.

## Populated migration source-build run (2026-10-19)

ChaptarrNG commit `12f290bde5c04d29202775c0e9eeafb5fc20608f` was built through
the checked-in `Dockerfile.build`, and SeerrNG was at commit
`fdc439aa9` (the migrated-record assertion added by that commit). The shared
fixture created one author, one mixed-format book, two editions, and synthetic
library files. ChaptarrNG started normally and converted the source from schema
158 to schema 121, retaining the source backup. With `HARDCOVER=true`, its
persisted config contained `HardcoverEnabled=true`.

SeerrNG's real `ReadarrAPI` read both the audiobook and ebook through their
format-specific facades and checked their Hardcover book identities. The run
also verified restricted-key access, denied administrative and unrelated
command routes, and completed the pending add/poll/cancel lifecycle. ChaptarrNG
reported `0.9.978.0`; the importer suite passed 43/43, Compose config
validation, `node --check`, and Prettier checks passed. The `.m4b` and `.epub`
files are placeholders, not playable samples. The separate six-case M4B test
validates synthetic processing and fallback only. This is synthetic
source-build evidence; it does not establish operator-data acceptance,
representative M4B compatibility, pre-14 schemas, PostgreSQL operator data, or
installed channel/rollback acceptance. No release or bridge was published.

## Earlier source-build request-lifecycle run (2026-10-19)

The prior checked-in Compose run built ChaptarrNG PR #131 head
`7cfb5d877fd2a49ea92840693ca8e57457e4dfc2` (application snapshot
`0612cb4c90b75818e5349eb868cc0638825de02a`) and SeerrNG source
`b810153c7d180a1d50388c9db6ae72195d1ab56e`. It started from an empty
disposable database and validated the checked-in `Dockerfile.build` path and
synthetic add/poll/cancel flow. The populated migration run above supersedes
that empty-library check for synthetic converted records.

## Earlier artifact-based run (2026-10-19)

Both `scripts/chaptarrng-integration-smoke.mjs` and
`integration/seerrng-request-lifecycle.mjs` passed against the isolated Linux
candidate from Build run
[39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373)
and SeerrNG source commit `b810153c`. The run verified the empty paged library,
the restricted-key boundary, and SeerrNG adapter add/poll/cancel behavior. Both
scripts also passed through the isolated Compose stack using a temporary
runtime image assembled from that prebuilt artifact with `--no-build`. That
earlier run did not validate the checked-in `Dockerfile.build` path; the later
source-build run above does.
