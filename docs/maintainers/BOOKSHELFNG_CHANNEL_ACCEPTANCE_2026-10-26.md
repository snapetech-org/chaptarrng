# BookshelfNG channel acceptance — 2026-10-26

## Result

The local 0.9.978 candidate passed installed handoff acceptance on Debian, Fedora, Arch, Windows Chocolatey, FreeBSD standalone, Docker, Helm, AppImage, Flatpak, and Co-op Cloud Swarm paths described below. The shared standalone launcher also passed a Linux runtime harness covering No, headless Manual, Yes, health, source backup, and forced rollback. A local Cloudron-image runtime rehearsal passed after fixing its command handoff. StartOS passed an installed x86_64 test-package update with manual defer and automatic migration; it remains a synthetic, test-key rehearsal. The optional choice is presented at the package prompt, a terminal-backed desktop or standalone launch, or an explicit platform setting, depending on the channel. pacman uses a deferred two-transaction handoff because pacman scriptlets cannot cancel an active transaction.

This is **not 100% release-channel acceptance**. No replacement artifact was published, and several package feeds contain no BookshelfNG predecessor. StartOS still needs the official signed package, aarch64 acceptance, and package-manager rollback. Cloudron platform updates, Unraid, canonical Co-op Cloud recipe/Backupbot acceptance, Windows standalone No-choice hosted validation, and external catalog installations remain open. Release gates remain closed.

## Choice behavior

| Channel | Does it support y/n? | No behavior | Yes behavior | Acceptance |
| --- | --- | --- | --- | --- |
| Debian / APT | Yes | Pre-install hook exits before staging; apt/dpkg cancels the candidate transaction and leaves the predecessor active. | Installs the same-name candidate, migrates during service startup, keeps the source backup, and supports tested rollback. | Installed Debian 13 VM, public BookshelfNG 0.4.21.71 to local 0.9.978; No, Yes, health, data, and rollback passed. |
| Fedora / DNF / RPM | Yes | RPM pre-install hook exits before staging; DNF cancels the transaction and leaves the predecessor active. Detached ask mode fails closed before staging. | Installs the same-name candidate, migrates under the service environment, checks HTTP/database health, and keeps the source backup. | Fedora 44 VM with SELinux enforcing, BookshelfNG 0.4.21.71 to local 0.9.978; interactive No/Yes, headless rejection, service health, data, and rollback passed. RPM scriptlets read the controlling terminal because DNF redirects scriptlet stdin even during terminal-backed installs. |
| Arch / AUR / pacman | Yes | pacman cannot cancel a transaction from a package scriptlet. The package transaction completes, then the hook restores the saved BookshelfNG app, unit, and launcher, leaves the database untouched, and records a deferred marker. The next package transaction asks again. | The next accepted transaction installs the ChaptarrNG runtime, converts the database, retains the BookshelfNG source backup, and starts the existing service. | Arch systemd VM: 0.4.21.71 to 0.9.978-2 with No, then 0.9.978-3 with Yes; schema 158 to 124, health and restore checks passed. |
| Windows / Chocolatey | Yes | Chocolatey cancels before the service is stopped or application files are staged; 0.4.21.70 remains installed and running. | Upgrades the bookshelfng identity and replaces the executable while retaining the NSSM service name, LocalSystem identity, data directory, service arguments, and source database backup. | Windows Server 2025 VM: public 0.4.21.70 to local 0.9.978; No and Yes passed. Chocolatey exited 0 for Yes; /ping and migration health check passed. |
| AppImage | Yes in its terminal-backed launcher | Defers database conversion; it does not undo a downloaded or replaced AppImage. The user can retry later. | Converts on launch, retains the source database, and supports restore. | The desktop entry sets `Terminal=true`. Local same-identity .71 to .978 rehearsal passed with synthetic schema-158 data, custom paths, health, and rollback. No is a data deferral. The candidate is unsigned and unpublished. |
| Flatpak | Yes in its terminal-backed launcher | Defers database conversion; it does not undo a Flatpak deployment. The user can retry later. | Converts on launch, retains the source database, and supports restore inside the sandbox. | The desktop entry sets `Terminal=true`. Local same-app-ID .71 to .978 rehearsal passed with synthetic schema-158 data, custom paths, health, and rollback. No is a data deferral. The candidate is unpublished. |
| Helm / Kubernetes | No interactive terminal in the pod | Ask mode defers conversion and keeps the old data in place; operator action and a pod restart are required to retry. | The candidate pod converts, starts, and passes the configured health check; a rollback Job restores the source database. | Kind installed upgrade from the existing BookshelfNG chart tag main-v0.4.21.71 passed No/defer, Yes, and explicit rollback. Candidate chart and image are not published. |
| Docker / Compose | Yes with an attached TTY; no in headless/Compose startup | TTY `n` defers conversion. Headless Ask writes a deferred marker, leaves the database and config byte-identical, and does not serve app `/ping`; the new container waits for operator action. | TTY `y` or a restart in automatic mode converts on first start, checks migration and app health, retains the source backup, and serves the existing endpoint. | Exact-head local candidate image passed interactive TTY No/Yes, headless Ask/defer, automatic handoff, `/ping`, migration check, and injected health-failure rollback. Image aliases and external pull/recreate delivery remain unpublished. |
| StartOS | No y/n prompt; package configuration uses an automatic/manual choice | Set Manual through the package action and restart; conversion is deferred and the source database remains byte-identical. | Set Automatic and restart; the replacement migrates, retains its source backup, passes database checks, and serves `/ping`. | StartOS 0.4.0.3 x86_64 VM: test-only SDK3 `bookshelfng` package `0.4.21:1` to synthetic candidate `0.9.979:1`; manual defer and automatic update passed with `/books` and `/downloads` canaries. This is not acceptance of the official signed package or public BookshelfNG .71 StartOS package; aarch64, package-manager rollback, and external-repository delivery remain untested. |
| Unraid | No y/n prompt in the template update flow | Set migration mode to Manual in the template before applying the image update; conversion is deferred until Automatic is selected and the container is restarted. | Set Automatic and restart; the image performs migration on startup. | XML/settings contract and draft PR checks passed; no Unraid Community Applications installation or rollback test was available. |
| Cloudron | No terminal prompt; the administrator can hold the app update or set manual/automatic mode | Set `BOOKSHELFNG_MIGRATION_MODE=manual` before applying the package update; the proxy keeps `/ping` healthy and shows the recovery instructions while the database remains unchanged. | Apply the update with automatic mode to convert on startup; Cloudron custom app environment can override the image default. | Exact-head Cloudron-image runtime passed manual defer and automatic conversion, including proxy liveness, source preservation, migration health, and backup. The candidate's first build exposed a missing inherited app command; the Dockerfile now passes it explicitly. Actual Cloudron platform install/update/backup/restore remains untested. |
| Co-op Cloud | No interactive y/n in Swarm. To choose No, keep the BookshelfNG stack deployed. Deploying the candidate with `manual` defers conversion but the new service does not serve `/ping` until the operator switches to Automatic and redeploys. | A Manual candidate update keeps the schema-158 database byte-identical and records `deferred`; the app endpoint is unavailable while held. | Redeploy the same stack in Automatic mode; the compatibility image converts, validates, retains a source backup, and serves `/ping`. | Isolated Debian 13 / Docker 26.1 Swarm upgraded the pinned BookshelfNG .71 source image to the exact-head .978 candidate. `bookshelfng_app` and all three volume names stayed stable. Manual preserved the database SHA-256 `f126ce2ae615dd6e898a446a2b122bc90ae9d10ea9a9ab2090cf0ca90830a1a1`, schema 158, and media canaries. Automatic reached schema 124, passed SQLite checks, kept the source backup and canaries, and served `/ping`. An injected unhealthy update paused as configured; restoring the three-volume snapshot and redeploying .71 returned healthy BookshelfNG at schema 158. The `backupbot.backup=true` label and full-volume archive/restore were verified; the official Backupbot CLI and canonical maintainer acceptance remain open. |
| Self-hosting catalogs | No common y/n contract | Depends on each catalog's UI and image replacement behavior. | Compose candidate preserves the bookshelfng service and volume identities. | Local Compose contract only; CasaOS, ZimaOS, Umbrel, TrueNAS, Cosmos, CapRover, Portainer, and other external listings were not updated or accepted. |
| macOS standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No or set `BOOKSHELFNG_MIGRATION_MODE=manual`; keep the prior app and service path. | Answer Yes or set `automatic`; the wrapper stages migration, starts ChaptarrNG, and checks health. The operator switches the launcher/service after acceptance. | Hosted run [40270599787](https://github.com/snapetech-org/chaptarrng/actions/runs/40270599787) passed on macOS x64 and arm64 against checksum-pinned public .71 predecessors: No, headless Manual, Yes, health, source backup, and forced rollback. It publishes no assets and validates the handoff runtime; the one-time app/service path switch remains manual. |
| FreeBSD standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No; the candidate exits without changing predecessor config/data. Restart BookshelfNG if it was stopped for the handoff. | Answer Yes; migration completes, ChaptarrNG starts, and health passes. The operator switches the launcher/service after acceptance. | FreeBSD 15.1 VM: installed BookshelfNG 0.4.21.71 to the exact local 0.9.978 archive; No, Yes, health, source backup, and forced-failure rollback passed. |
| Windows standalone migration installer | Yes, through its explicit Migrate BookshelfNG action and the migration preview's Yes/No confirmation; closing the window defers | Close without selecting Migrate, or answer No at preview; BookshelfNG remains installed. | Select Migrate, review the preview, then answer Yes to run backup, conversion, service handoff, and health checks. | Hosted Windows run [40218131519](https://github.com/snapetech-org/chaptarrng/actions/runs/40218131519) installed the checksum-pinned .71 service, forced automatic rollback, completed the real installer handoff, and exercised explicit restore. The installer source is unchanged since that run. This campaign adds a No-at-preview service round-trip to the Windows smoke; its hosted result is pending. Chocolatey remains a separate channel. |

## Windows Chocolatey evidence

The final local bridge package SHA-256 is 2bf0c0de3771266801aba5c0b3c6d970a323f537faa5eb4eeb1334bb373546d1. Its self-contained Windows archive SHA-256 is 16cd7eadbadf6d66a86aa68421de91dafe3986adb86b86539cc26c9d8ea1ac3a.

After Yes, the BookshelfNG service ran Chaptarr.Console.exe under the existing LocalSystem identity and data path, with the original -nobrowser and -data service arguments. The HTTP endpoint returned {"status":"OK"}, the migration health check exited 0, and the channel canary remained channel-canary=preserve-me.

The copied target database passed SQLite integrity_check with zero foreign-key violations and contained 4 authors, 9 format-specific books, and 4 book files at Chaptarr migration 124. The retained source backup passed the same checks with 4 authors, 6 books, 4 book files, and BookshelfNG schema 158. The target DB SHA-256 is a204610f73b14b2492af10d1cfe51406f859272b597aced853b0fe800f419201; the source backup SHA-256 is 03f78da296ba13c5d485f5590fcda875a432e65eea5910fb55820642a78d942e.

The Windows run exposed two bridge bugs and both are fixed: utility-mode errors could wait for Enter despite -exitimmediately, and captured utility stdout could be mistaken for the returned process exit code. The Console exit path now honors -exitimmediately for all failures, and the Chocolatey wrapper writes diagnostics separately while returning only the integer exit code. The PowerShell regression passed in the Windows VM.

An earlier test input archive was framework-dependent and lacked its .NET runtime. That failure is preserved as test-input evidence; the final test used the self-contained archive above and completed the real migration.

## Docker installed-image evidence

The exact-head local image is `localhost:15001/chaptarrng-channelqa:0.9.978-exact-head-ttyfinal`, image ID `sha256:4ba7f6b3fe858821db056d0370dc8b5ed8a45c9efae138b5b5202f7f488bf76d`. It layers the current `Chaptarr.dll` (SHA-256 `70e3881f696ab5879ff6e02839d2adff7f464eb81e0cf88ac3f57b49f94433f2`) and the final compatibility supervisor onto the dependency-matched local 0.9.978 image. The image was not pushed.

Interactive Docker `ask` displayed one `[y/N]` question. A single `n` wrote `manual-review`, left no pending or failed marker, kept the BookshelfNG database byte-identical at SHA-256 `53901555272388bc56157239ab674e3493dbcbcfd70557e0a468b0df9cf091b7`, preserved `config.xml` byte-for-byte, and did not serve app `/ping`. A single `y` on a fresh source fixture authorized conversion. The supervisor reported successful migration and startup health, `/ping` returned OK, and the read-only migration health check exited 0. The target passed SQLite integrity and foreign-key checks at schema 124 with 4 authors, 9 books, and 4 book files; its retained schema-158 source backup passed the same checks with 4 authors, 6 books, and 4 book files.

The final image was retested headless. Ask mode wrote `manual-review` and kept the database and config byte-identical; automatic mode converted the fixture, retained the source backup, passed the read-only migration check, and served `/ping`. The injected 30-second startup-health failure restored the schema-158 source database byte-for-byte, wrote `rolled-back`, retained the failed schema-124 target, and left `/ping` unavailable. Both database copies passed integrity checks with zero foreign-key violations. The complete current-image record is `/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/docker-exact-head-ttyfinal-acceptance.json`.

A separate automatic-start fixture injected a port mismatch (`CHAPTARR_EXPECTED_PORT=8788`, saved port `8787`). The supervisor wrote `rolled-back`, stopped serving `/ping`, restored the source database byte-for-byte to the original SHA-256, and retained the converted target separately. Both databases passed integrity and foreign-key checks. The complete record is `/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/docker-exact-head-fixed-acceptance.json`; the Ask, successful automatic, and rollback transcripts sit beside it.

Two TTY test harness issues were fixed before acceptance: GNU `timeout` needed `--foreground` to keep its child in the Docker terminal's process group, and Console utilities needed `-exitimmediately` to avoid waiting for Enter on a utility error before the y/n question. A later Cloudron-image test found that its replacement entrypoint had no inherited `CMD`; the candidate now passes the Chaptarr command explicitly. The final runs used fresh fixtures and waited for the explicit supervisor success or rollback message before collecting results.

## Cloudron image-wrapper evidence

A local Cloudron candidate image layered the package Dockerfile, nginx proxy, and Cloudron entrypoint onto the exact-head application image. With `BOOKSHELFNG_MIGRATION_MODE=manual`, the app wrote `manual-review`, preserved the source database byte-for-byte, returned HTTP 200 from the proxy `/ping`, and served the migration instructions for normal requests. With automatic mode, the inner app listened on port 8788 behind the stable 8787 proxy, migration health passed, `/ping` and the app route worked, and the read-only migration check passed. The target database passed integrity at schema 124 with 4 authors, 9 books, and 4 book files; the retained schema-158 source backup passed integrity. Cloudron supports custom per-app environment values, so the operator can select manual mode before update and automatic mode when accepting conversion. This is a local image-wrapper rehearsal, not an installed Cloudron platform update/backup/restore acceptance.

## FreeBSD standalone installed acceptance

The FreeBSD 15.1-RELEASE-p6 x86_64 VM installed the checksum-verified
BookshelfNG 0.4.21.71 predecessor and the standard-builder ChaptarrNG 0.9.978
archive. Candidate SHA-256:
`f60819a90a65c4deadd4eb44befa5ffd4f1dca0b7022fbe647ac2f2e9310398d`.

The bundled `scripts/bookshelfng-standalone-launch.sh` asked `[y/N]` on a
terminal. No preserved the predecessor's config and database byte-for-byte
and left the canary unchanged. After restarting the predecessor service,
BookshelfNG `/ping` returned OK.
Yes migrated the same data path; the replacement returned OK, reached schema
124, passed SQLite integrity and foreign-key checks, and retained a byte-exact
schema-158 source backup. The migrated database contained 4 authors, 9 books,
9 editions, and 2 series. A forced replacement health failure on a fresh source
fixture wrote `rolled-back`, restored the source database byte-for-byte, kept
the failed schema-124 target for diagnosis, and allowed the BookshelfNG service
to restart and pass `/ping`.

The runtime rehearsal exposed two FreeBSD-specific startup issues. The launcher
now selects an installed OpenSSL ABI (`.17` or `.35`) when the runtime's older
default ABI is absent, and repairs the bundle's `libe_sqlite3.so` link from the
system `sqlite3` library. The first missing-SQLite failure is preserved in
`freebsd-installed-candidate-preflight-missing-sqlite.log`; the final No/Yes,
rollback, and verification transcripts are retained in the task evidence
directory. The archive was rebuilt through the standard builder and its digest
matches the installed candidate.

## StartOS installed update acceptance

The StartOS 0.4.0.3 x86_64 VM first installed a test-only SDK3 package with
package ID `bookshelfng`, version `0.4.21:1`, and the public BookshelfNG .71
image digest. The candidate kept that ID and updated it to test version
`0.9.979:1` using the corrected wrapper. The installed `.s9pk` SHA-256 was
`312a0c956dac70d82623aecd866c0bfa2ae5b29f1749e4d1fdfbd5e9c4147bce`. The
package action set Manual and the
same test volume was upgraded: the schema-158 source database remained
byte-identical, no `/ping` endpoint started, and the log recorded deferred
migration. The subsequent Automatic setting migrated the same source to
schema 124, retained a schema-158 backup, passed SQLite integrity and foreign
key checks, and returned a healthy `/ping`. Unique canaries in `/books` and
`/downloads` survived the update. Evidence is in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/startos-corrected-manual-readonly.log`,
`startos-corrected-automatic-acceptance.log`, and `startos-volume-canaries.log`.

This run used a synthetic test package signed with the task-local key and a
local x86_64 candidate image. It confirms the StartOS package update and data
handoff in that test environment, but not the external maintainer's signed
package, aarch64 image, published package repository, or package-manager
rollback. The wrapper now inherits the image entrypoint with
`sdk.useEntrypoint()` and uses StartOS's port readiness check; the former
`/init` child-process invocation and missing in-image health script both
blocked startup.

## Other package checks

The 19-case legacy-channel Node contract suite passed after adding FreeBSD launcher/runtime, Windows standalone installer opt-in, macOS workflow, and StartOS wrapper checks. The publication-free hosted macOS handoff run passed on x64 and arm64. Docker compatibility defaults and the exact-head installed-image interactive/headless Ask/automatic/rollback acceptance passed. The Cloudron manual/automatic runtime rehearsal passed after fixing the missing command. The AUR prompt test passed No/defer/restore and a later Yes retry. The Fedora 44 RPM prompt transaction regression passed interactive No/Yes and detached headless Ask; Debian passed headless rejection, apt No/Yes, and downgrade. Linux shell syntax, workflow policy, and `git diff --check` passed.

The shared Linux standalone handoff harness used a checksum-verified public BookshelfNG .71 predecessor (SHA-256 `4197d53b757ebe99642fb8f4213f963dd54641401d1f188ab6daadcb3b024d5d`) and the exact ChaptarrNG candidate archive (SHA-256 `359897785599f850c2264d2cc2aad96c1fa1e69ffd4ad1b6e98e3d783c75d364`). No preserved the predecessor and restarted its `/ping`; headless Manual deferred without changing source files; Yes migrated data to schema 124, passed integrity and foreign-key checks, retained a logical source backup, and returned healthy `/ping`. An injected replacement-health failure restored the source database and restarted BookshelfNG while retaining the failed target for diagnosis. The rerun also confirms the harness now closes the PTY after a stalled Ctrl+C shutdown and reaps the candidate. This Linux harness validates the shared launcher/supervisor; Linux standalone archives do not gain a new update channel from it. Full output: `/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/standalone-handoff-stop-fix-v6.log`.

Windows evidence is recorded at /var/tmp/chaptarrng-channel-acceptance-20261026/evidence/windows-final-acceptance.json and the full guest transcript is windows-server-2025-chocolatey-acceptance.log in the same evidence directory. Helm evidence is in helm-final2-acceptance.json and helm-final2-rollback-acceptance.json. Native VM package and service logs are retained beside them.

Co-op Cloud Swarm source, Manual defer, Automatic migration, paused failed
update, and archive restore evidence is under
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/coop-cloud-swarm-*`.
The VM used the local checksum-pinned .71 predecessor and exact-head .978
candidate; the full pre-migration volume snapshot and SSH key were removed
after the run.

## Remaining release blockers

- Debian/PPA and Fedora/COPR publisher paths remain closed; direct VM package acceptance does not prove a PPA or COPR feed upgrade. The public BookshelfNG COPR repository had no packages for the tested Fedora series.
- The Chocolatey feed rejection is unresolved. The local package passed, but no public package was pushed.
- The AUR update candidate is local and unpublished.
- Docker alias/image delivery, Helm OCI chart delivery, AppImage/Flatpak publication, and external catalog updates remain gated.
- StartOS passed the local x86_64 VM handoff, but the official signed package, aarch64 runtime, external repository, and package-manager rollback remain open. Cloudron needs an installed platform update/backup/restore rehearsal. Co-op Cloud passed local Swarm update, defer, failure-pause, and full-volume restore; canonical recipe/Backupbot acceptance remains open. Unraid and external catalog platform handoffs still need their own update and rollback tests.
- The Windows standalone installer passed hosted Yes, injected-failure rollback, and explicit restore; the new No-at-preview service round-trip is pending its hosted Windows run.
- macOS legacy installs need a one-time manual bootstrap; hosted x64 and arm64 runtime handoff acceptance now passes. The old BookshelfNG updater cannot be redirected from this repository. FreeBSD installed No/Yes/rollback acceptance is complete, but the one-time launcher/service path switch remains an operator action.
- Representative operator data and production provider/API workflows are outside this channel-package campaign.

Do not mark the global channel gate complete or publish these candidates until the blocked delivery and platform checks above are closed or the supported-channel scope is explicitly changed.
