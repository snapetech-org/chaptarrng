# BookshelfNG on StartOS

This wrapper preserves the existing BookshelfNG StartOS package ID and volume
contract while running ChaptarrNG's BookshelfNG-compatible Hardcover image on
x86_64 and aarch64 systems. The release handoff builder replaces the image
placeholder in `startos/manifest/index.ts` with the exact published
multi-architecture digest. The package persists the application database at
`/config`, the library at `/books`, and download files at `/downloads`.

This candidate uses StartOS SDK 3 and requires StartOS 0.4.0.2 or newer. Update
StartOS before installing it on an older device.

```sh
npm ci
npm run check
npm run build
make
start-sdk verify s9pk bookshelfng.s9pk
```

Build the digest-pinned source candidate, then verify and sideload the resulting
`.s9pk` on StartOS. Test backup, update, `/ping`, and restore to the prior
BookshelfNG image before marketplace submission. The current submission path
is the wrapper-repo link plus package details sent to Start9.

Application source: <https://github.com/snapetech-org/chaptarrng>

## Security scanning

Run scripts/security-scan-local.sh for local Semgrep Community Edition and applicable Trivy scans. Public GitHub Actions workflows run these scans on changes and weekly, alongside any existing CodeQL workflow. See docs/security-scanning.md for offline setup and updates. Run scripts/install-security-hooks.sh once to enable the staged Semgrep pre-commit hook.
