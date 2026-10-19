# Integration smoke tests

The SeerrNG smoke stack builds the ChaptarrNG candidate, starts both services
on an isolated Compose network, and checks capability discovery, paged book
reads, the restricted service key, and a real SeerrNG adapter add/poll/cancel
cycle for an unavailable synthetic author.

Set the inputs in the shell; the Compose file does not need a `.env` file:
the SeerrNG checkout must have dependencies installed and its server built
first (`pnpm install --frozen-lockfile && pnpm build:server` from that checkout).
The smoke container mounts both the source tree and its installed `node_modules`
read-only so it can run SeerrNG's integration script with the same dependencies.
It also mounts this integration directory read-only; its second script imports
the compiled SeerrNG `ReadarrAPI` adapter from `/seerrng/dist` and sends an
isolated pending-request lifecycle through ChaptarrNG's HTTP API. The ChaptarrNG
container receives a disposable `/books` volume for its ebook root folder.

```sh
export SIXLABORS_LICENSE_FILE="$HOME/Downloads/sixlabors.lic"
export SEERRNG_SOURCE="/path/to/seerrng"
export SEERRNG_NODE_MODULES="$(realpath "$SEERRNG_SOURCE/node_modules")"
export CHAPTARR_GLOBAL_API_KEY="$(openssl rand -hex 24)"
export CHAPTARR_SEERR_API_KEY="$(openssl rand -hex 24)"

docker compose -p chaptarrng-seerrng-smoke \
  -f integration/seerrng-smoke.compose.yml \
  up --build --abort-on-container-exit --exit-code-from smoke
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

## Recorded run (2026-10-19)

Both `scripts/chaptarrng-integration-smoke.mjs` and
`integration/seerrng-request-lifecycle.mjs` passed against the isolated Linux
candidate from Build run
[39239940373](https://github.com/snapetech-org/chaptarrng/actions/runs/39239940373)
and SeerrNG source commit `b810153c`. The run verified the empty paged library,
the restricted-key boundary, and SeerrNG adapter add/poll/cancel behavior. The
Both scripts then passed through the isolated Compose stack using a temporary
runtime image assembled from that prebuilt artifact. The run used `--no-build`,
so it did not validate the checked-in `Dockerfile.build` path.
