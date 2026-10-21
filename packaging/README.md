# Native package channels

Status checked 2026-10-21. ChaptarrNG [`v0.9.977`](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.977)
is the latest published release; [main](https://github.com/snapetech-org/chaptarrng/tree/main) includes a candidate-
only BookshelfNG Debian bridge builder. GitHub Release archives are the payload for
native packages; the standard ChaptarrNG container is built separately from
the same application source and migration code. Package recipes add service
and filesystem integration. See the
[ChaptarrNG v0.9.977 release](https://github.com/snapetech-org/chaptarrng/releases/tag/v0.9.977)
for the published archives, checksums, and source RPMs.

The old-identity package recipes remain disabled candidates. Do not install a
bridge package or point ChaptarrNG at BookshelfNG appdata until migration,
rollback, feature-parity, and channel acceptance gates close. Normal startup
can convert a supported BookshelfNG database automatically.

For current BookshelfNG migration and legacy-channel acceptance status, see
[BookshelfNG retirement status](../docs/BOOKSHELFNG_RETIREMENT.md).

## Package identities

| Channel | New ChaptarrNG install | Existing BookshelfNG identity | Data path during upgrade |
| --- | --- | --- | --- |
| Debian / Ubuntu `.deb` | No ChaptarrNG `.deb` is currently published | Candidate package keeps `bookshelfng` | `/var/lib/bookshelfng` |
| Arch AUR | `chaptarrng-bin` | `bookshelfng-bin` | `/var/lib/bookshelfng` |
| Fedora COPR | `slskdn/chaptarrng` | `slskdn/bookshelfng` | `/var/lib/bookshelfng` |
| Chocolatey | `chaptarrng` | `bookshelfng` | `%ProgramData%\BookshelfNG\data` |
| Docker | `ghcr.io/snapetech-org/chaptarrng` | Legacy image aliases | Existing `/config` mount |
| Helm | ChaptarrNG `charts/bookshelfng` with a versioned publisher for the existing OCI identity; unpublished while the release freeze is active | `oci://ghcr.io/snapetech/bookshelfng/bookshelfng` | Same Helm release, namespace, `/config` PVC, and volume name |

Legacy package recipes keep the old package identity, service name, and data
path so a package upgrade can start the normal ChaptarrNG database migration.
They are a bridge candidate, not proof that every existing install can be
upgraded safely.

The Debian candidate recipe and local package builder are in
`packaging/legacy/debian/` and
`packaging/scripts/build-bookshelfng-deb-bridge.sh`. The builder consumes the
standard Linux x64 release archive and produces a `.deb` under the old package
identity. The package-content contract checks the payload, service and config
paths, and maintainer-script handoff. It also verifies that the shipped
`bookshelfng.env` has the same SHA-256 as the final public `.71` package, then
runs an isolated dpkg round-trip and an apt upgrade/downgrade in a pinned
Debian Trixie container.

A local candidate built from the checksum-verified ChaptarrNG `v0.9.977` Linux
x64 archive upgraded the checksum-verified BookshelfNG `0.4.21.71-1` package
and downgraded back. Both dpkg and apt transactions preserved an edited
`/etc/bookshelfng/bookshelfng.env` and a data canary. The apt transaction
resolved real Debian dependencies, including `libicu76` and
`ca-certificates`; it installed the CA bundle and completed without a conffile
prompt. A separate Debian Trixie systemd-container rehearsal started the
actual `.71` service from a fresh data directory, upgraded it to the local
`.977` candidate, and confirmed the active service restarted and returned
`/ping` successfully. The run exercised the previously active but disabled
unit case. It then restored the pre-upgrade data copy, downgraded to `.71`, and
confirmed that service returned `/ping` again. Edited config and data canaries
survived both transactions. This was not populated BookshelfNG operator data;
the candidate started against the `.71` package's fresh empty database. The
isolated dpkg transaction bypassed dependency checks and mocked systemd. There
is no apt repository update path; the candidate has not been exercised on a
real host or published to a GitHub Release or Ubuntu PPA. Keep this channel
disabled until representative-data rollback and channel acceptance pass.

The stable release workflow now attaches the legacy `.deb` and checksum only
after an explicit bridge request and when the migration and both release
readiness gates are true. Those gates remain closed; this workflow has not
published a bridge asset. No Launchpad PPA bridge is configured yet.

The legacy environment-file conffile is byte-for-byte identical to the final
public BookshelfNG package. Keeping that checksum stable avoids an unnecessary
dpkg prompt during unattended upgrades; add new migration guidance to external
documentation instead of changing this file.

## Standalone archive coverage

The published `v0.9.977` archives cover `linux-x64`, `linux-arm64`, and
`win-x64`. The current source release workflow now covers the full BookshelfNG
archive matrix: `linux-x64`, `linux-x86`, `linux-arm64`, `linux-musl-x64`,
`linux-musl-arm64`, `freebsd-x64`, `osx-x64`, `osx-arm64`, `win-x64`, and
`win-x86`. Linux x86 and FreeBSD use separately built, pinned .NET runtime
packs. A no-publication validation workflow builds and verifies all ten
candidate archives. Run
[39582093706](https://github.com/snapetech-org/chaptarrng/actions/runs/39582093706)
passed eight archive targets; Linux x86 and FreeBSD failed because `cp -a`
tried to restore ownership and timestamps on preexisting SDK files. ChaptarrNG
`main` now omits those attributes when merging custom packs, and its focused
overlap regression passes locally. Follow-up exact-head validation
[39595241427](https://github.com/snapetech-org/chaptarrng/actions/runs/39595241427)
is queued to check all ten archives. The CI artifacts are temporary and do not publish
assets. These archives are not public until a later release is approved. Keep
BookshelfNG in service until the full migration and channel acceptance gates
close.
Keep the BookshelfNG installation in service until a replacement channel is
validated or that platform is explicitly retired.

## Public status

| Channel | ChaptarrNG | BookshelfNG | Remaining check |
| --- | --- | --- | --- |
| Debian / Ubuntu | No ChaptarrNG `.deb` is published | The final BookshelfNG `.deb` is `0.4.21.71`; an isolated dpkg upgrade/downgrade to the candidate and back preserved a modified conffile and data canary | Validate dependency resolution, real systemd startup, database migration/restore, and the PPA/direct-download update path; no candidate is published |
| AUR | `chaptarrng-bin 0.9.977-1` is published | `bookshelfng-bin 0.4.21.71-1` remains public; a local `0.9.977-1` candidate passed isolated pacman upgrade/downgrade, synthetic populated migration/restore, and an active systemd service handoff rehearsal | Validate with representative operator data and host-specific service settings before publishing the candidate |
| COPR | `chaptarrng 0.9.977-1` built successfully for Fedora 43, 44, 45, and Rawhide x86_64 | Legacy trigger skipped; current `slskdn/bookshelfng` repo metadata has no packages for Fedora 43, 44, 45, or Rawhide x86_64 | A local Fedora 44 RPM transaction rehearsal upgraded the published `.71` RPM to a `.977` bridge candidate and downgraded back, preserving config/data canaries. `systemctl` was mocked and RPM dependencies were bypassed; no live COPR/DNF or real service test is established. |
| Chocolatey | `.977` built and passed the hosted BookshelfNG-to-ChaptarrNG upgrade smoke, but the push returned HTTP 403; the live feed has no `chaptarrng` entry | Public `bookshelfng 0.4.21.70` remains | Resolve publisher authorization, publish the new identity, then test the old identity with real package data and rollback |
| Docker | `ghcr.io/snapetech-org/chaptarrng:0.9.977`, `:latest`, and `:main` resolve to the same multi-architecture digest | Mutable BookshelfNG aliases unchanged. Candidate compatibility targets map `latest`, `main`, and `hardcover` to native Hardcover mode and `softcover` to Goodreads mode. | Validate all three manifests and old-alias digest routing on exact-head CI before enabling the bridge |
| Helm | A same-name chart and versioned publisher target the existing BookshelfNG OCI identity. Both release-readiness gates, the migration bridge gate, and an explicit bridge opt-in are required. Local build/render and hosted resource-identity checks pass. | No chart is published; the BookshelfNG OCI chart remains unchanged. Existing installs can keep the same chart source after acceptance. | Test a real PVC/GitOps upgrade and rollback before publishing or switching existing installations |

Core release validation passed on Linux, macOS, Windows, and PostgreSQL. The
Windows Chocolatey build and package-transition smoke also passed: it
installs the public BookshelfNG package, upgrades through the ChaptarrNG
legacy recipe, checks service and data/backup handling, and verifies that
uninstall preserves data. This does not validate conversion of a populated
operator database. The candidate BookshelfNG package itself was not published.

Before a release can publish, the workflow validates AUR private-key
usability, package-scoped ChaptarrNG and BookshelfNG COPR webhook formats,
and Chocolatey key shape. Invalid optional publisher settings skip that
channel without writing credential values to the workflow summary. The main
release preflight also validates the Discord webhook before artifact or image
publishing. These checks validate configuration shape, not live publisher
authorization or channel acceptance.

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
updated. The legacy package and image bridges remain disabled. Re-enabling
them requires both release-readiness gates, the migration bridge gate, and an
explicit bridge opt-in.

The current legacy COPR repository metadata contains no package entries for
Fedora 43, 44, 45, or Rawhide x86_64. A local Fedora 44 transaction test
installed the published BookshelfNG `.71` RPM, upgraded it to a locally built
`.977` `bookshelfng` RPM candidate from the current bridge spec, and downgraded
to `.71`. The test preserved a config override and data canary. A mocked
`systemctl` verified stop-then-start scriptlet ordering during upgrade; the
downgrade was performed after an explicit stop and followed by a manual start.
The RPM transaction used `--nodeps`, did not run a real systemd service, and
did not migrate a database. This validates the package identity and file
handling only. It does not validate an install or upgrade through COPR/DNF;
there is no currently published old-identity package in that repository.

On 2026-10-18, the AUR build script built both `chaptarrng-bin 0.9.977-1`
and a local `bookshelfng-bin 0.9.977-1` candidate against the public Linux
release archive. Package-content checks confirmed the ChaptarrNG executable
and package metadata in both packages, plus the legacy service and
`/etc/bookshelfng/bookshelfng.env` path in the candidate. An isolated,
network-disconnected Arch container installed the published AUR version
`0.4.21.71-1`, upgraded the same package identity to the local `.977` candidate,
and downgraded back to `.71`. Pacman retained the modified environment file
with a `.pacnew` default, preserved a synthetic data canary, and restored the
old executable on downgrade.

The installed `.977` package then previewed and converted a synthetic populated
BookshelfNG migration-158 SQLite database to ChaptarrNG migration 120. The
preview left the source bytes unchanged; apply retained the source backup and
passed SQLite integrity and foreign-key checks. Restore returned the active
database to migration 158, retained the converted database as a backup, and
passed the same checks.

A second rehearsal booted systemd in a network-isolated Arch container. It
started the public `.71` BookshelfNG package against the same populated fixture,
then upgraded the active service to the local `.977` candidate. The pacman hook
stopped the BookshelfNG process before replacement; the post-upgrade hook
reloaded systemd and started ChaptarrNG, whose normal startup converted the
database to migration 120. The migration-158 source backup passed integrity and
foreign-key checks; the target had two format-specific book rows and passed the
same checks. The environment override was retained with a `.pacnew` default,
and the data canary remained intact.

With the service stopped, restore preview preserved both database hashes.
Explicit restore returned the database to migration 158 and retained the
converted migration-120 database as a backup; both passed integrity and
foreign-key checks. Pacman downgrade restored `.71`, and starting the service
opened the restored database. The test container had no network, so
`systemd-networkd-wait-online` was masked only inside that disposable container
to let its `network-online.target` complete. This validates synthetic package
and service lifecycle behavior; representative operator data and host-specific
settings remain open. The candidate remains unpublished.

## Migration behavior and limits

The default ChaptarrNG app-data location differs from BookshelfNG's
native-package locations. A legacy package recipe preserves the old location
so the standard ChaptarrNG app can find `readarr.db` and run its migration.
The published `v0.9.977` converter accepts BookshelfNG migration-158 SQLite
databases and bundles the BookshelfNG SQLite migration chain. It upgrades
recognized source schemas on a disposable copy before conversion; the raw
snapshot remains available for rollback. The current `.977` pre-upgrade check
rejects source schemas before migration 14 because they still have `Blacklist`.
PR #126's version-aware table check and authentic migration-1
rollback fixture passed hosted Linux, macOS, Windows, and PostgreSQL migration
checks, but the fix is still source-only. No follow-up release will be cut
until the full retirement gates close. Populated synthetic migration-22 tests
cover conversion and rollback; real older-schema operator data remains
untested. Unknown or newer schemas, PostgreSQL sources, and two populated
source databases fail closed in the published `.977` build.
The ChaptarrNG-owned Docker handoff helper is source-only today. The release
workflow will include it in future Linux standalone archives once publication
is approved; the public `.977` archives do not contain it. See the
[migration guide](../docs/MIGRATE_FROM_BOOKSHELFNG.md#chaptarrng-owned-docker-handoff-helper)
for its backup, preview, apply, and rollback behavior.
The separate PostgreSQL candidate is limited to one migration-158 schema. Earlier smoke run 39127070836 on code head 21c41ad5 timed out during application startup before readiness; this was not a confirmed converter assertion failure. PR #131 later merged to `main` on 2026-10-20. Its exact-head Build [39374140961](https://github.com/snapetech-org/chaptarrng/actions/runs/39374140961) passed Linux, macOS, Windows, PostgreSQL migration, release-note, and native-runner receipt checks. Those PostgreSQL checks use synthetic fixtures only; they do not establish PostgreSQL operator-data support, and the candidate remains unpublished.
The importer keeps records with unresolved formats in both catalogs
without monitoring, writes a private review file, retains the source backup,
and supports validated rollback.

Docker Compose, Unraid, and other container users can change the image to
`ghcr.io/snapetech-org/chaptarrng` and retain `/config`, media/download mounts,
ports, UID/GID, and environment settings. BookshelfNG stores no primary-provider
marker alongside bare numeric IDs, so direct migration must preserve
`HARDCOVER=true` or explicitly set `HARDCOVER=false`; the converter now fails
closed if neither mode is available. The legacy mutable-image bridge will map
the old `hardcover`/`main`/`latest` aliases to a Hardcover-enabled compatibility
image and `softcover` to a Goodreads-mode image. These targets and alias changes
are source-only, have not been published, and remain gated. Pinned versions and
image digests still need an operator change. A repointed tag does not change a
running container.

The BookshelfNG Windows WPF installer still installs the old application and
does not hand off its `Readarr` service. ChaptarrNG now contains a source-only
WPF migration-installer candidate in `distribution/windows/installer-net`.
Windows CI builds it as an internal artifact; it is not published. It supports
one SQLite database, asks for provider mode, creates a verified retained
backup, previews before apply, then switches to the `Chaptarr` service and
checks the saved data path and health endpoint. PostgreSQL and multi-database
installations are rejected. Native Windows migration/rollback, representative
data, and channel acceptance remain open, so it is not approved for operator
use. See the [Windows migration procedure](../docs/MIGRATE_FROM_BOOKSHELFNG.md#windows-standalone-service-migration).

BookshelfNG's in-app updater still checks its existing upstream update service,
which ChaptarrNG cannot redirect. Automatic migration must therefore use a
validated image/package identity bridge, or operators must change their install
source. Debian/PPA, Flatpak/AppImage, Helm, third-party catalogs, and
pinned/offline installs need an explicit handoff or retirement decision.

The BookshelfNG Cloudron and StartOS packages still reference their legacy
`.37` image, and no ChaptarrNG update has been tested for either package.
The Co-op Cloud recipe remains an unsubmitted draft. These package definitions
must not be pointed at ChaptarrNG until their data identity, update behavior,
and rollback are accepted or the channel is explicitly retired. See the
[BookshelfNG retirement status](../docs/BOOKSHELFNG_RETIREMENT.md).

## Work required before enabling the legacy bridge

- Validate the AUR candidate with representative operator data and
  host-specific service settings; the Fedora `.71` to `.977` RPM transaction
  rehearsal passed with canaries, but the legacy COPR repo is empty and live
  DNF/COPR upgrade acceptance remains open. Test the old-identity Chocolatey
  upgrade in a disposable Windows system. Preserve service settings and data
  paths, retain the source backup, and verify rollback without losing later
  writes.
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
