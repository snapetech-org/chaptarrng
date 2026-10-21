# BookshelfNG legacy distribution candidates

These files are source candidates for existing BookshelfNG identities. They
preserve desktop IDs, Cloudron app identity, container names, service ports,
data directories, and named volumes where the channel allows an in-place
handoff. They do not publish packages, change external catalogs, or approve
operator migration.

The container recipes reference the source-only
`ghcr.io/snapetech-org/chaptarrng:bookshelf-hardcover` target. Before any
channel update, publish an approved compatibility image, resolve its immutable
multi-architecture digest, update the candidate to that digest, and complete
that channel's install, update, backup, and rollback acceptance. AppImage and
Flatpak candidates use the verified ChaptarrNG Linux x64 standalone archive
staged by `packaging/scripts/prepare-bookshelf-desktop-candidates.sh`:

```sh
bash packaging/scripts/prepare-bookshelf-desktop-candidates.sh \
  "$RUNNER_TEMP/ChaptarrNG-${VERSION}-linux-x64.tar.gz" \
  "$RUNNER_TEMP/bookshelfng-desktop-candidates"
```

The staging helper requires the archive's adjacent `.sha256` sidecar, rejects
archive links and unsafe paths, and refuses to overwrite its output directory
or accept ChaptarrNG `v0.9.977`, whose published converter predates native
Hardcover identity handling. Use version `0.9.978` or newer. To build both
packages locally, install `appimagetool`, `flatpak-builder`, and the Flatpak
26.08 runtime and SDK, then run:

```sh
bash packaging/scripts/build-bookshelf-desktop-candidates.sh \
  "$RUNNER_TEMP/ChaptarrNG-${VERSION}-linux-x64.tar.gz" \
  "$RUNNER_TEMP/bookshelfng-desktop-candidate-build"
```

The builder writes checksummed AppImage and Flatpak artifacts plus a local
Flatpak repository. It does not install or publish the bundle. The runtime and
SDK must already be installed; the builder does not change the host's Flatpak
installation.

StartOS package code lives in the separate `bookshelfng-startos` repository.
The StartOS note here documents its package and image contract; it does not
replace that external package update or device validation.
