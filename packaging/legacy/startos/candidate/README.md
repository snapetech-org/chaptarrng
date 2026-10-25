# BookshelfNG on StartOS

This wrapper preserves the existing BookshelfNG StartOS package ID and volume
contract while running ChaptarrNG's BookshelfNG-compatible Hardcover image on
x86_64 and aarch64 systems. The release handoff builder replaces the image
placeholder in `startos/manifest/index.ts` with the exact published
multi-architecture digest. The package persists the application database at
`/config`, the library at `/books`, and download files at `/downloads`.

This candidate uses StartOS SDK 3 and requires StartOS 0.4.0.2 or newer. Update
StartOS before installing it on an older device.

`icon.png` is the ChaptarrNG project logo from this repository. The daemon
explicitly enables compatibility startup, keeps port `8787`, and selects the
Hardcover provider mode used by this package candidate.

```sh
OUTPUT_DIR=/path/to/new/startos-artifacts ./build-startos-package.sh
```

The release handoff bundle supplies a digest-pinned source candidate and a
StartOS package version derived from the matching ChaptarrNG release. The
ChaptarrNG repository's
`packaging/scripts/build-bookshelf-startos-artifacts.sh` helper verifies that
bundle and builds both supported architectures inside the configured StartOS
packaging workspace. It checks the signing-key path without reading or copying
the key and writes signed `.s9pk` files, SHA-256 sidecars, and a release
manifest to a new output directory. The ChaptarrNG release builder does not
hold or use the external package signing key. Inspect each generated package,
then sideload and test install, backup, update, `/ping`, and restore to the
prior BookshelfNG image before marketplace submission.

Automatic first-start migration is enabled by default. Use the package's
**Configure BookshelfNG migration** action to defer conversion for a manual
preview and apply; the setting is saved in the StartOS backup volume.

Application source: <https://github.com/snapetech-org/chaptarrng>

## Security scanning

Run scripts/security-scan-local.sh for local Semgrep Community Edition and applicable Trivy scans. Public GitHub Actions workflows run these scans on changes and weekly, alongside any existing CodeQL workflow. See docs/security-scanning.md for offline setup and updates. Run scripts/install-security-hooks.sh once to enable the staged Semgrep pre-commit hook.
