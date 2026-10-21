# Standalone archive builds

ChaptarrNG's source release matrix targets the ten runtime identifiers used by
the final BookshelfNG archive workflow:

| Runtime identifiers | Runtime pack source |
| --- | --- |
| `linux-x64`, `linux-arm64`, `linux-musl-x64`, `linux-musl-arm64`, `osx-x64`, `osx-arm64`, `win-x64`, `win-x86` | .NET 10 SDK 10.0.402 |
| `linux-x86`, `freebsd-x64` | Pinned .NET 10 runtime packs built by `packaging/scripts/build-extra-platform-runtime-packs.sh` |

## Validate without publishing

`.github/workflows/standalone-asset-validation.yml` builds the UI once, builds
all ten self-contained archives, verifies each archive and checksum, and
stores temporary build products only on the GitHub runner. It uses version
`0.0.0` and does not create a GitHub release, package, image, installer, or
update-channel handoff. The exact-head result must pass before these candidate
assets can be considered for a future release.

## Build the custom runtime packs

The Linux x86 and FreeBSD build uses Docker, .NET SDKs `10.0.110`, `10.0.111`,
and `10.0.402`, the pinned `v10.0.12` .NET source, and a FreeBSD runtime
archive verified against its checked-in SHA-256. Use two unique, absolute,
task-scoped directories that do not already exist:

```sh
DOTNETVERSION=10.0.402 \
PLATFORM_RUNTIME_WORK_DIR=/tmp/chaptarrng-platform-runtime-work-task-id \
PLATFORM_RUNTIME_PACKS_OUTPUT=/tmp/chaptarrng-platform-runtime-packs-task-id \
bash packaging/scripts/build-extra-platform-runtime-packs.sh
```

The work directory contains the cloned .NET source trees and build outputs.
The output directory contains runtime packages, SDK metadata, and the install
helper. Keep those paths separate from user data and remove only the exact
task-scoped paths after their consumers finish.

The SDK's reference packs can have a newer servicing version than the pinned
runtime sources. The pack builder copies the SDK's installed reference-pack
versions, while the Linux x86 and FreeBSD standalone publish explicitly targets
runtime `10.0.12` to match the custom runtime packages.

The runtime-pack installer merges files into the SDK already installed on the
runner. It preserves file modes and symlinks but deliberately does not restore
ownership or source timestamps, which may be unsupported for shared SDK files.
`packaging/scripts/test-install-extra-platform-runtime-packs.sh` covers an
overlapping pack file so the copy path is checked against a preexisting SDK.

## Build a standalone archive

Build the frontend first so `_output/UI/index.html` exists. The archive builder
requires .NET SDK `10.0.402`, `SIXLABORS_LICENSE_KEY`, and an output directory.
For Linux x86 and FreeBSD, download or build the custom runtime-pack artifact,
then set `CUSTOM_RUNTIME_PACKS_DIR` to its `packages` directory:

```sh
CUSTOM_RUNTIME_PACKS_DIR=/path/to/platform-runtime-packs/packages \
DOTNETVERSION=10.0.402 \
bash packaging/scripts/build-standalone.sh 0.0.0 freebsd-x64 /path/to/task-output
```

The builder creates a `.tar.gz` or `.zip` archive and a `.sha256` sidecar in
the requested output directory. Its temporary bundle is created with
`mktemp` under `${TMPDIR:-/tmp}` and removed by an exit trap.

## Publication gate

The runtime matrix and build workflows are source-only until the release and
migration acceptance gates close. ChaptarrNG `v0.9.977` still contains only
the three archives listed in the release notes; no expanded archive set or
BookshelfNG channel bridge is public.
