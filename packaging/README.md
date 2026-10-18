# Native package channels

Status checked 2026-10-18. ChaptarrNG [`v0.9.977`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.977)
is the latest published release. GitHub Release archives are the payload for
native packages; the standard ChaptarrNG container is built separately from
the same application source and migration code. Package recipes add service
and filesystem integration. See the
[ChaptarrNG v0.9.977 release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.977)
for the published archives, checksums, and source RPMs.

For current BookshelfNG migration and legacy-channel acceptance status, see
[BookshelfNG retirement status](../docs/BOOKSHELFNG_RETIREMENT.md).

## Package identities

| Channel | New ChaptarrNG install | Existing BookshelfNG identity | Data path during upgrade |
| --- | --- | --- | --- |
| Arch AUR | `chaptarrng-bin` | `bookshelfng-bin` | `/var/lib/bookshelfng` |
| Fedora COPR | `slskdn/chaptarrng` | `slskdn/bookshelfng` | `/var/lib/bookshelfng` |
| Chocolatey | `chaptarrng` | `bookshelfng` | `%ProgramData%\BookshelfNG\data` |
| Docker | `ghcr.io/snapetech-org/chaptarrng` | Legacy image aliases | Existing `/config` mount |

Legacy package recipes keep the old package identity, service name, and data
path so a package upgrade can start the normal ChaptarrNG database migration.
They are a bridge candidate, not proof that every existing install can be
upgraded safely.

## Public status

| Channel | ChaptarrNG | BookshelfNG | Remaining check |
| --- | --- | --- | --- |
| AUR | `chaptarrng-bin 0.9.977-1` is published | `bookshelfng-bin 0.4.21.71-1` remains on the BookshelfNG payload | Test an old-package upgrade through conversion and rollback |
| COPR | `chaptarrng 0.9.977-1` built successfully for Fedora 43, 44, 45, and Rawhide x86_64 | Legacy trigger skipped | Test the old package identity in a Fedora upgrade before enabling its bridge |
| Chocolatey | `.977` built and passed the hosted BookshelfNG-to-ChaptarrNG upgrade smoke, but the push returned HTTP 403; the live feed has no `chaptarrng` entry | Public `bookshelfng 0.4.21.70` remains | Resolve publisher authorization, publish the new identity, then test the old identity with real package data and rollback |
| Docker | `ghcr.io/snapetech-org/chaptarrng:0.9.977`, `:latest`, and `:main` resolve to the same multi-architecture digest | Mutable BookshelfNG aliases unchanged | Verify legacy registry write access and digest equality before enabling the bridge |

Core release validation passed on Linux, macOS, Windows, and PostgreSQL. The
Windows Chocolatey build and package-transition smoke also passed: it
installs the public BookshelfNG package, upgrades through the ChaptarrNG
legacy recipe, checks service and data/backup handling, and verifies that
uninstall preserves data. This does not validate conversion of a populated
operator database. The candidate BookshelfNG package itself was not published.

The `v0.9.977` release and standard image were published successfully. The
ChaptarrNG Chocolatey package built and passed its package-transition smoke,
but Chocolatey rejected the publish with HTTP 403. The workflow reported
publisher credentials as configured; the feed rejection still needs to be
resolved before a public ChaptarrNG package exists. The legacy BookshelfNG
identity was not submitted because the migration bridge is disabled. No
credential value is recorded here.

The release workflow validated source RPMs for both identities and triggered
the standard ChaptarrNG COPR webhook for `0.9.977-1`. COPR reports the build
succeeded for Fedora 43, 44, 45, and Rawhide x86_64. The
legacy BookshelfNG COPR trigger was skipped because the migration bridge is
disabled. The ChaptarrNG AUR
identity was published at `0.9.977-1`; the BookshelfNG AUR identity was not
updated. The `CHAPTARRNG_MIGRATION_BRIDGE_ENABLED` Actions variable is `false`,
so old package IDs and image aliases do not redirect to ChaptarrNG.

## Migration behavior and limits

The default ChaptarrNG app-data location differs from BookshelfNG's
native-package locations. A legacy package recipe preserves the old location
so the standard ChaptarrNG app can find `readarr.db` and run its migration.
The published `v0.9.977` converter accepts BookshelfNG migration-158 SQLite
databases and bundles the BookshelfNG SQLite migration chain. It upgrades
recognized source schemas on a disposable copy before conversion; the raw
snapshot remains available for rollback. The current `.977` pre-upgrade check
rejects source schemas before migration 14 because they still have `Blacklist`.
A fix and authentic migration-1 coverage are being validated for a follow-up
release. Populated synthetic migration-22 tests cover conversion and rollback;
real older-schema operator data remains untested. Unknown or newer schemas,
PostgreSQL sources, and two populated source databases fail closed.
The importer keeps records with unresolved formats in both catalogs
without monitoring, writes a private review file, retains the source backup,
and supports validated rollback.

Docker Compose, Unraid, and other container users can change the image to
`ghcr.io/snapetech-org/chaptarrng` and retain `/config`, media/download mounts,
ports, UID/GID, and environment settings. Unraid has a ChaptarrNG template.
Only mutable aliases can be repointed; pinned versions and image digests need
an operator change. A repointed tag does not change a running container.

BookshelfNG's in-app updater still checks its existing upstream update service,
which ChaptarrNG cannot redirect. Automatic migration must therefore use a
validated image/package identity bridge, or operators must change their install
source. Debian/PPA, Flatpak/AppImage, standalone installer definitions, Helm,
third-party catalogs, and pinned/offline installs need an explicit handoff or
retirement decision.

## Work required before enabling the legacy bridge

- Test AUR, COPR, and Chocolatey old-identity upgrades in disposable systems.
  Preserve service settings and data paths, convert the migration-158 fixture,
  retain the source backup, and verify rollback without losing later writes.
- Resolve Chocolatey HTTP 403 authorization, publish the `chaptarrng` identity,
  and confirm its public checksum matches the tested package.
- Publish and validate the migration-1-to-13 alias fix before recommending
  `.977` for databases earlier than migration 14.
- Verify old GHCR/Docker Hub destinations, credentials, and per-platform
  manifest digests before changing any mutable image aliases.
- Complete the separate [BookshelfNG feature-parity roadmap](https://github.com/snapetech-org/bookshelfng/blob/main/docs/maintainers/bookshelfng-parity-roadmap.md),
  or explicitly accept and document the behavior that will not move.
- Decide which BookshelfNG install channels will be retired without a redirect,
  especially its upstream in-app updater and unsupported third-party channels.

The database conversion and published-image migration smoke test are complete
for the supported schema. The published BookshelfNG identity bridges and full
feature/channel retirement are not.
