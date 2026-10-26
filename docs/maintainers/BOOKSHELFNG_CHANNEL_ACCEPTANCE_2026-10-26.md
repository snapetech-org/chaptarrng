# BookshelfNG channel acceptance — 2026-10-26

## Result

The local 0.9.978 candidate passed installed handoff acceptance on Debian, Fedora, Arch, Windows Chocolatey, Linux x64 systemd standalone, FreeBSD standalone, Docker, Helm, AppImage, Flatpak, and Co-op Cloud Swarm paths described below. The Chocolatey No/Yes VM pass used the then-public BookshelfNG 0.4.21.70 predecessor; the current public 0.4.21.76 predecessor and private-JIT bridge handoff remain open. The shared Linux launcher harness also passed No, headless Manual, Yes, health, source backup, and forced rollback; it is separate from the Linux tarball’s systemd bootstrap, which is tested below. Cloudron 10.1.6 passed an installed .4.21 to .4.23 package update, Manual defer, native backup/restore, Automatic migration, and source-database plus old-package rollback. StartOS passed x86_64 and aarch64 test-package updates covering Manual defer, automatic migration, health, and source rollback; these remain synthetic, test-key rehearsals. A local Unraid-template Docker rehearsal passed manual defer, automatic migration, health, and forced rollback; it does not count as Community Applications or host acceptance. Channels expose the migration choice through a package prompt, terminal-backed launch, or platform setting where supported; pacman uses a deferred two-transaction handoff because its scriptlets cannot cancel an active transaction.

This is **not 100% release-channel acceptance**. The current 0.9.978 BookshelfNG replacement artifacts have not been published, and several package feeds contain no BookshelfNG predecessor. StartOS still needs the official signed package, external repository delivery, and package-manager rollback acceptance. Cloudron feed/catalog publication, Unraid Community Applications/host update and rollback acceptance, canonical Co-op Cloud recipe acceptance, and external catalog installations remain open. The old Cloudron `.4.21` package also cannot back up its persistent directories; that legacy backup gap is covered only after the new package with backup hooks is installed. Release gates remain closed.

## Choice behavior

| Channel | Does it support y/n? | No behavior | Yes behavior | Acceptance |
| --- | --- | --- | --- | --- |
| Debian / APT | Yes | Pre-install hook exits before staging; apt/dpkg cancels the candidate transaction and leaves the predecessor active. | Installs the same-name candidate, migrates during service startup, keeps the source backup, and supports tested rollback. | Installed Debian 13 VM, public BookshelfNG 0.4.21.71 to local 0.9.978; No, Yes, health, data, and rollback passed. |
| Fedora / DNF / RPM | Yes | RPM pre-install hook exits before staging; DNF cancels the transaction and leaves the predecessor active. Detached ask mode fails closed before staging. | Installs the same-name candidate, migrates under the service environment, checks HTTP/database health, and keeps the source backup. | Fedora 44 VM with SELinux enforcing, BookshelfNG 0.4.21.71 to local 0.9.978; interactive No/Yes, headless rejection, service health, data, and rollback passed. RPM scriptlets read the controlling terminal because DNF redirects scriptlet stdin even during terminal-backed installs. |
| Linux x64 standalone / systemd | Yes; the one-time bootstrap now asks `[y/N]` before acquiring its handoff lock. | No, blank, or EOF returns before the lock, service stop, or file writes. Noninteractive Ask fails closed; unattended replacement requires `--migration-mode automatic`. | Yes stages the checksum-verified archive, migrates the existing service data, switches the executable, checks health, and retains rollback files. | Historical Debian 12 VM acceptance passed migration, health, data, and forced rollback before this prompt was added. The new No/Yes prompt regression is in the Linux JIT Build job but has not run; the installed VM handoff must be replayed on approved private JIT. |
| Arch / AUR / pacman | Yes | pacman cannot cancel a transaction from a package scriptlet. The package transaction completes, then the hook restores the saved BookshelfNG app, unit, and launcher, leaves the database untouched, and records a deferred marker. The next package transaction asks again. | The next accepted transaction installs the ChaptarrNG runtime, converts the database, retains the BookshelfNG source backup, and starts the existing service. | Arch systemd VM: 0.4.21.71 to 0.9.978-2 with No, then 0.9.978-3 with Yes; schema 158 to 124, health and restore checks passed. |
| Windows / Chocolatey | Yes | Chocolatey cancels before the service is stopped or application files are staged; the installed predecessor remains active. | Upgrades the bookshelfng identity and replaces the executable while retaining the NSSM service name, LocalSystem identity, data directory, service arguments, and source database backup. | Windows Server 2025 VM: public 0.4.21.70 to local 0.9.978; No and Yes passed. Chocolatey exited 0 for Yes; `/ping` and migration health check passed. The current public predecessor is 0.4.21.76; its full No/Yes/rollback handoff has not passed on private Windows JIT. |
| AppImage | Yes in its terminal-backed launcher | Defers database conversion; it does not undo a downloaded or replaced AppImage. The user can retry later. | Converts on launch, retains the source database, and supports restore. | The desktop entry sets `Terminal=true`. Local same-identity .71 to .978 rehearsal passed with synthetic schema-158 data, custom paths, health, and rollback. No is a data deferral. The candidate is unsigned and unpublished. |
| Flatpak | Yes in its terminal-backed launcher | Defers database conversion; it does not undo a Flatpak deployment. The user can retry later. | Converts on launch, retains the source database, and supports restore inside the sandbox. | The desktop entry sets `Terminal=true`. Local same-app-ID .71 to .978 rehearsal passed with synthetic schema-158 data, custom paths, health, and rollback. No is a data deferral. The candidate is unpublished. |
| Helm / Kubernetes | No interactive terminal in the pod | Ask mode defers conversion and keeps the old data in place; operator action and a pod restart are required to retry. | The candidate pod converts, starts, and passes the configured health check; a rollback Job restores the source database. | Kind installed upgrade from the existing BookshelfNG chart tag main-v0.4.21.71 passed No/defer, Yes, and explicit rollback. Candidate chart and image are not published. |
| Docker / Compose | Yes with an attached TTY; no in headless/Compose startup | TTY `n` defers conversion. Headless Ask writes a deferred marker, leaves the database and config byte-identical, and does not serve app `/ping`; the new container waits for operator action. | TTY `y` or a restart in automatic mode converts on first start, checks migration and app health, retains the source backup, and serves the existing endpoint. | Exact-head local candidate image passed interactive TTY No/Yes, headless Ask/defer, automatic handoff, `/ping`, migration check, and injected health-failure rollback. Image aliases and external pull/recreate delivery remain unpublished. |
| StartOS | No y/n prompt; package configuration uses an automatic/manual choice | Set Manual through the package action and start the candidate; conversion is deferred, the app remains unavailable, and the schema-158 source stays unchanged. Run the documented Console preview/apply, then switch back to Automatic and restart. | Set Automatic and start the candidate; the replacement previews, converts, retains its source backup, passes database checks, and serves `/ping`. A failed health check restores the source database and leaves ChaptarrNG stopped. | StartOS 0.4.0.3 x86_64 and aarch64 VMs passed test-key SDK3 package updates. ARM64 used synthetic predecessor `.4.21:3` to candidate `.9.979:1`; Manual defer, Console preview/apply, Automatic conversion, and injected health-failure rollback all passed with `/books` and `/downloads` canaries. The predecessor fixture wraps the pinned BookshelfNG Readarr 0.4.21.37 ARM64 image with a direct service command because its s6 `/init` requires PID 1. Official signed `.s9pk`, public `.71` package, external repository, and package-manager rollback remain unaccepted. |
| Unraid | No y/n prompt in the template update flow | Set migration mode to Manual in the template before applying the image update; conversion is deferred until Automatic is selected and the container is restarted. | Set Automatic and restart; the image performs migration on startup. | Local Docker rehearsal against BookshelfNG 0.4.21.71 and a local ChaptarrNG 0.9.978 image passed manual defer, migration, `/ping`, retained source data, settings and media canaries, and forced rollback/recovery using the legacy template's mount and environment contract. This was not run in Unraid Community Applications or on an Unraid host; image publication remains open. |
| Cloudron | No terminal prompt; the administrator can hold the app update or set manual/automatic mode | Set `BOOKSHELFNG_MIGRATION_MODE=manual` before applying the package update; the proxy keeps `/ping` healthy and shows recovery instructions while the database remains unchanged. | Apply the update with automatic mode to convert on startup; Cloudron custom app environment can override the image default. | Cloudron 10.1.6 installed `.4.21` to `.4.23`; Manual defer, native backup/restore, Automatic migration, and source-database plus old-package rollback passed. The `.4.21` pre-update backup does not include persistent directories; `.4.23` adds backup/restore hooks. Feed/catalog publication remains open. |
| Co-op Cloud | No interactive y/n in Swarm. To choose No, keep the BookshelfNG stack deployed. Deploying the candidate with `manual` defers conversion but the new service does not serve `/ping` until the operator switches to Automatic and redeploys. | A Manual candidate update keeps the schema-158 database byte-identical and records `deferred`; the app endpoint is unavailable while held. | Redeploy the same stack in Automatic mode; the compatibility image converts, validates, retains a source backup, and serves `/ping`. | Isolated Debian 13 / Docker 26.1 Swarm upgraded the pinned BookshelfNG .71 source image to the exact-head .978 candidate. `bookshelfng_app` and all three volume names stayed stable. Manual preserved the database SHA-256 `f126ce2ae615dd6e898a446a2b122bc90ae9d10ea9a9ab2090cf0ca90830a1a1`, schema 158, and media canaries. Automatic reached schema 124, passed SQLite checks, kept the source backup and canaries, and served `/ping`. An injected unhealthy update paused as configured; restoring the three-volume snapshot and redeploying .71 returned healthy BookshelfNG at schema 158. The official Backupbot II 2.4.0 image (`sha256:870277cdba4b91ad6771184a7e208ba29151b951e39c68b4a3fd6f8d35252feb`) backed up those same volume identities using the recipe label. Its restore recovered a corrupted schema-158 database byte-for-byte, passed SQLite checks, restored all three volume canaries, and left the service mounts unchanged. Abra 0.13.0-beta deployed an isolated recipe with the same service and volume identities, created and listed the snapshot, then restored the corrupted database byte-for-byte and restored all three canaries after the app was scaled to zero. The app restarted with unchanged mounts. Canonical recipe acceptance remains open. |
| Self-hosting catalogs | No common y/n contract | Depends on each catalog's UI and image replacement behavior. | Compose candidate preserves the bookshelfng service and volume identities. | Local Compose contract only; CasaOS, ZimaOS, Umbrel, TrueNAS, Cosmos, CapRover, Portainer, and other external listings were not updated or accepted. |
| macOS standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No or set `BOOKSHELFNG_MIGRATION_MODE=manual`; keep the prior app and service path. | Answer Yes or set `automatic`; the wrapper stages migration, starts ChaptarrNG, and checks health. The operator switches the launcher/service after acceptance. | Hosted run [40270599787](https://github.com/snapetech-org/chaptarrng/actions/runs/40270599787) passed on macOS x64 and arm64 against checksum-pinned public .71 predecessors: No, headless Manual, Yes, health, source backup, and forced rollback. It publishes no assets and validates the handoff runtime; the one-time app/service path switch remains manual. |
| FreeBSD standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No; the candidate exits without changing predecessor config/data. Restart BookshelfNG if it was stopped for the handoff. | Answer Yes; migration completes, ChaptarrNG starts, and health passes. The operator switches the launcher/service after acceptance. | FreeBSD 15.1 VM: installed BookshelfNG 0.4.21.71 to the exact local 0.9.978 archive; No, Yes, health, source backup, and forced-failure rollback passed. |
| Windows standalone migration installer | Yes, through its explicit Migrate BookshelfNG action and the migration preview's Yes/No confirmation; closing the window defers | Close without selecting Migrate, or answer No at preview; BookshelfNG remains installed. | Select Migrate, review the preview, then answer Yes to run backup, conversion, service handoff, and health checks. | Hosted Windows run [40218131519](https://github.com/snapetech-org/chaptarrng/actions/runs/40218131519) installed the checksum-pinned .71 service, forced automatic rollback, completed the real installer handoff, and exercised explicit restore. The installer source is unchanged since that run. Hosted Windows run [40274632625](https://github.com/snapetech-org/chaptarrng/actions/runs/40274632625) completed the No-at-preview service round-trip; No kept BookshelfNG installed, Yes completed the upgrade, and injected-failure rollback, explicit restore, service cleanup, and selected-data-directory checks passed. Chocolatey remains a separate channel. |

## Standalone release artifact coverage

These runtime identifiers are separate release payloads even when they share a
package channel. An installed handoff for one RID does not establish that the
other runtime payloads launch or migrate correctly. The Windows migration
installer is a separate x64 artifact.

Four of the ten release RIDs have installed handoff behavior evidence
(`linux-x64`, `osx-x64`, `osx-arm64`, and `freebsd-x64`); none has the required
current private-JIT replay. Six RIDs have no installed BookshelfNG handoff
record. The Windows migration-installer evidence is tracked separately from
the `win-x64` archive.

| Release RID / artifact | Existing handoff evidence | Remaining acceptance |
| --- | --- | --- |
| `linux-x64` | Local 0.9.978 archive installed over public BookshelfNG .71 on Debian 12; migration, health, data preservation, and forced rollback passed before the new prompt. | The new `[y/N]` choice regression is added to the Linux JIT Build job; it is not yet run. Replay No/Yes plus installed rollback on approved private JIT. |
| `linux-arm64` | StartOS aarch64 test-package handoff passed; it wraps a different StartOS package/runtime. | No installed handoff with the standalone `linux-arm64` release archive is recorded. |
| `linux-musl-x64` | Release payload validation only. | No installed BookshelfNG handoff is recorded. |
| `linux-musl-arm64` | Release payload validation only. | No installed BookshelfNG handoff is recorded. |
| `linux-x86` | Release payload validation only. | No installed BookshelfNG handoff is recorded. |
| `osx-x64` | Historical hosted handoff run built and checksum-verified the exact candidate archive, then passed No, Manual defer, Yes, health, source backup, and rollback. | Replay the exact-archive workflow on approved private JIT. |
| `osx-arm64` | Historical hosted handoff run built and checksum-verified the exact candidate archive, then passed No, Manual defer, Yes, health, source backup, and rollback. | Replay the exact-archive workflow on approved private JIT. |
| `win-x64` | Historical hosted migration-installer Yes/No, service handoff, health, and rollback acceptance passed. | Private Windows JIT replay; validate the standalone archive separately from the migration installer. |
| `win-x86` | Release payload validation only. | No installed BookshelfNG handoff is recorded. |
| `freebsd-x64` | Local exact 0.9.978 archive installed over public BookshelfNG .71 on FreeBSD 15.1; No, Yes, health, and rollback passed. | Replay on approved private JIT. |

The public/private JIT route must also be checked before any of these
historical VM or hosted results count toward final campaign acceptance.

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

A local Cloudron candidate image layered the package Dockerfile, nginx proxy, and Cloudron entrypoint onto the exact-head application image. With `BOOKSHELFNG_MIGRATION_MODE=manual`, the app wrote `manual-review`, preserved the source database byte-for-byte, returned HTTP 200 from the proxy `/ping`, and served the migration instructions for normal requests. With automatic mode, the inner app listened on port 8788 behind the stable 8787 proxy, migration health passed, `/ping` and the app route worked, and the read-only migration check passed. The target database passed integrity at schema 124 with 4 authors, 9 books, and 4 book files; the retained schema-158 source backup passed integrity. Cloudron supports custom per-app environment values, so the operator can select manual mode before update and automatic mode when accepting conversion. This records the wrapper-only rehearsal; installed platform acceptance is recorded in the next section.

## Cloudron installed platform evidence

Cloudron 10.1.6 updated app `tech.snapetech.bookshelfng` from the BookshelfNG
`.4.21` package to the local ChaptarrNG `.4.23` candidate, using the local
`0.9.978-cloudron-backup-hooks-v2` application image. The app ID, port, and
`/config`, `/books`, and `/downloads` mounts were preserved. Manual mode left
the schema-158 database unchanged, kept `/ping` at HTTP 200, and showed the
recovery page for normal requests.

Native Cloudron backup task 22 completed. Its archive contains consistent
SQLite snapshots for the active `readarr.db`, `logs.db`, `cache.db`, and
`staging.db`, records absent `audioarr.db` and `chaptarr.db`, and includes the
three persistent directories. The archive checksum and all SQLite integrity
checks passed. After changing databases, config, and media canaries and adding
stale database files, native restore task 25 restored the original config and
canary hashes, schema-158 database, and file layout; the stale files were
removed.

Automatic mode then migrated schema 158 to 124, passed integrity and
foreign-key checks, served `/ping`, and retained a BookshelfNG source backup.
The internal `-restore-bookshelf` operation restored schema 158 with the
original author/book counts, after which Cloudron restored the `.4.21` package
and returned BookshelfNG to healthy `/ping`. This tested the supported rollback
sequence while preserving later evidence files.

The legacy `.4.21` pre-update Cloudron backup did not contain `/config`,
`/books`, or `/downloads`: that package had no backup hooks. The new hooks only
protect backups created after `.4.23` is installed. Full task evidence,
including the backup ID and archive/database checks, is in the
[Cloudron platform test record](CLOUDRON_PLATFORM_ACCEPTANCE_2026-10-26.md).
Supporting VM logs and snapshots are retained under
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/`. The VM image and
registry were local QA infrastructure; no candidate or feed was published.

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

The x86_64 and aarch64 StartOS 0.4.0.3 VMs used test-only SDK3 `.s9pk` files
with package ID `bookshelfng`. The earlier x86_64 run updated test package
`.4.21:1` to `.9.979:1`; its candidate SHA-256 was
`312a0c956dac70d82623aecd866c0bfa2ae5b29f1749e4d1fdfbd5e9c4147bce`. The ARM64
run updated synthetic predecessor `.4.21:3` to `.9.979:1`. The predecessor
package SHA-256 was
`e5f9318d17dae104f99d06cda29bfe9f4d9dbe10ca25164e90178fb2bf758c23`; the
candidate SHA-256 was
`77a0f9e0c1d916ed7594d6b33366819f67269684fd132ec446cda7d2777f5bde`.

On ARM64, the package action persisted Manual, the package supervisor held
startup, and the schema-158 source remained unchanged. The Console migration
preview passed, explicit apply converted to schema 124 and retained the
BookshelfNG backup, and switching to Automatic restarted the same package with
healthy `/ping`. Re-seeding from the retained schema-158 backup then proved
the Automatic first-start path. SQLite integrity and foreign-key checks passed;
the final database had 4 authors, 9 books, and 1 download client. The `/books`
and `/downloads` canary hashes remained
`cf812181855dbf277ee62c0d3fbd17be04e0aa3a501f8a3dd529e6189f47a127` and
`d8b55e0867c52c71515b6261127404b0e9a37849fd7124fc528414ab2befcd34`.

A final test package `.9.980:1` deliberately expected port `8788` while the
candidate listened on `8787`. After the bounded health failure, StartOS
restored the schema-158 source to the retained backup hash
`5ee42780c008cf49d829c41f916383c91a8dc8936b867593739d2fedc4e6d260`, retained
the failed schema-124 target separately, and left `/ping` unavailable. Both
databases passed integrity and foreign-key checks. The failure candidate SHA
was `74651a849c7cad66ae3e65cdd70f8327a3952a6583859ca0d59f16ccca3f553d`.

The ARM64 predecessor fixture wraps the public BookshelfNG Readarr 0.4.21.37
image with a direct service command because its s6 `/init` must run as PID 1;
it is not the public `.71` StartOS package. All packages used a task-local
signing key. The exact guest transcript, package logs, hashes, and offline
SQLite checks are retained under
`/var/tmp/chaptarrng-channel-acceptance-20261026/vms/startos-acceptance/arm64/`;
the evidence summary is
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/startos-arm64-acceptance.md`.
This verifies synthetic package identity, update, migration choice, health,
and automatic source rollback on ARM64. It does not validate the external
maintainer's signed package, official `.71` predecessor, repository delivery,
or StartOS package-manager rollback. The candidate continues to inherit the
image entrypoint with `sdk.useEntrypoint()` and uses StartOS's port readiness
check.

## Other package checks

The 19-case legacy-channel Node contract suite passed after adding FreeBSD launcher/runtime, Windows standalone installer opt-in, macOS workflow, and StartOS wrapper checks. The publication-free hosted macOS handoff run passed on x64 and arm64. Docker compatibility defaults and the exact-head installed-image interactive/headless Ask/automatic/rollback acceptance passed. The Cloudron manual/automatic runtime rehearsal passed after fixing the missing command. The AUR prompt test passed No/defer/restore and a later Yes retry. The Fedora 44 RPM prompt transaction regression passed interactive No/Yes and detached headless Ask; Debian passed headless rejection, apt No/Yes, and downgrade. Linux shell syntax, workflow policy, and `git diff --check` passed.

The shared Linux standalone handoff harness used a checksum-verified public BookshelfNG .71 predecessor and an earlier local ChaptarrNG candidate. No preserved the predecessor and restarted /ping; headless Manual deferred without changing source files; Yes migrated to schema 124, passed integrity and foreign-key checks, retained a logical source backup, and returned healthy /ping. An injected replacement-health failure restored BookshelfNG and retained the failed target for diagnosis. The rerun also confirms the harness closes the PTY after a stalled Ctrl+C shutdown and reaps the candidate. This validates the shared launcher/supervisor only; the Linux archive has its own systemd bootstrap, tested in the following section. Full output: /var/tmp/chaptarrng-channel-acceptance-20261026/evidence/standalone-handoff-stop-fix-v6.log.

### Linux x64 standalone archive and systemd bootstrap

A Debian 12 x86_64 VM ran the installed BookshelfNG 0.4.21.71 service under systemd with schema-158 fixture data, settings, a synthetic API-key canary, and a filesystem canary. The local ChaptarrNG 0.9.978 archive was built from e9900d8d120e2a952c12ba45af1eb8394fa8bf34; it was not published.

The installed bootstrap migrated the target to schema 124, passed SQLite integrity and foreign-key checks, preserved settings and canaries, retained a logical backup matching the source baseline, switched the service path to Chaptarr, and returned healthy HTTP 200. A forced failure after the real migration restored the exact schema-158 source, predecessor executable and service, and health while retaining the valid schema-124 target copy. The run found and fixed a staging-directory collision and a service-user read-permission failure in the candidate payload.

The final archive SHA-256 is 14d30e6e65cd17e3e030a28d8368a0b3fb135268673d2e91dd25dc2e0efbd843. The embedded bootstrap helper SHA-256 matches source: e5d02515a5d1564f301a556845461ababbebbf5b2ee689dc40123c18c76a4c2d. The predecessor archive SHA-256 is 4197d53b757ebe99642fb8f4213f963dd54641401d1f188ab6daadcb3b024d5d. Logs, test packages, and checksums are retained in /var/tmp/chaptarrng-channel-acceptance-20261026/evidence/linux-standalone-current-20261026/.

Windows evidence is recorded at /var/tmp/chaptarrng-channel-acceptance-20261026/evidence/windows-final-acceptance.json and the full guest transcript is windows-server-2025-chocolatey-acceptance.log in the same evidence directory. Helm evidence is in helm-final2-acceptance.json and helm-final2-rollback-acceptance.json. Native VM package and service logs are retained beside them.

Co-op Cloud Swarm source, Manual defer, Automatic migration, paused failed
update, and volume restore evidence is under
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/coop-cloud-swarm-*`.
The Backupbot II acceptance log is
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/coop-cloud-backupbot-acceptance.log`.
It records image digest `sha256:870277cdba4b91ad6771184a7e208ba29151b951e39c68b4a3fd6f8d35252feb`,
an official `backup create` and `restore --noninteractive`, and successful
restore of the schema-158 fixture (SHA-256
`53901555272388bc56157239ab674e3493dbcbcfd70557e0a468b0df9cf091b7`) plus
all three volume canaries. The VM used the local checksum-pinned .71
predecessor and exact-head .978 candidate; temporary VM disks and keys were
removed after the runs.

Abra acceptance is recorded in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/coop-cloud-abra-backup-restore-acceptance.log`.
Abra 0.13.0-beta was checksum-verified and used local server mode to deploy a
test recipe with the `bookshelfng_app` identity, production volume names, and
`backupbot.backup=true`. `abra app backup create`, `snapshots`, and `list`
succeeded. After the database and volume canaries were corrupted and the app
was scaled to zero, `abra app restore` recovered the schema-158 fixture
byte-for-byte, passed SQLite integrity and row-count checks, restored all
canaries, preserved service mounts, and restarted the app. The test recipe and
helper are retained in
`/var/tmp/chaptarrng-channel-acceptance-20261026/vms/coop-cloud-abra/`.

## Current-worktree revalidation — 2026-10-26 follow-up

This section records code-level evidence for the modified worktree at d43a005.
At that follow-up cutoff it did not include a new package build and installed
upgrade across the platform VMs. The later current-source Linux x64 systemd
package and installed handoff acceptance is recorded below.

The focused .NET migration/database set passed 299 tests, with 11 PostgreSQL
cases skipped because their external database service is unavailable. The
BookshelfNG migration-journal fixture passed 4/4 cases. I added
`BookshelfMultiDatabaseConverterFixture` and verified 3/3 cases: populated
identical databases merge without duplication and retain both originals,
preview changes neither source, and a conflicting setting blocks promotion.
Those tests found and fixed a rejected journal-count format; the focused build
also found and fixed two C# compile errors in the current migration changes.

The legacy-channel contract suite passed 19/19, the private runner policy test
passed 10/10, all 17 workflow YAML files parsed, and `git diff --check` passed.
The full core suite and a final combined migration rerun were stopped after
10 minutes and 5.5 minutes respectively without a test summary; neither result
is counted as a pass or failure. Logs are retained at
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/` with the
`dotnet-bookshelf-*-20261026-followup.log`,
`dotnet-bookshelf-*-20261026-incomplete.log`,
`dotnet-core-full-20261026-incomplete.log`,
`node-channel-contract-20261026-followup.log`,
`workflow-runner-policy-20261026-followup.log`,
`workflow-yaml-20261026-followup.log`, and
`diffcheck-20261026-followup.log` names. The interrupted importer fixture at
`/var/tmp/chaptarrng-channel-acceptance-20261026/tmp/bookshelf-importer-7c37a74f/`
was retained for diagnosis.

At the time of this follow-up, private-only workflow edits were local and no CI
or canary had been started. Those route changes were later committed to main
in ca676141 and 7cbb1d2e; current runner state and the C0 gate are recorded
in ORG_WIDE_PRIVATE_JIT_AUDIT_2026-10-26.md. This follow-up does not close
the signed-package, feed/catalog, Unraid, canonical Co-op Cloud, external
catalog, or private JIT rollout gates.

## Remaining release blockers

### PostgreSQL source migration integration check

The current local workspace's full BookshelfNG PostgreSQL-focused suite passed
29/29 tests against an isolated PostgreSQL 17.4 server. It covered source
conversion and rollback, database selection, lineage checks, and external dump
backup/validation using the host's PostgreSQL 18.6 client tools. The fixtures
left no reserved test databases or migration schemas behind. Logs and container
provenance are retained in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/postgresql-source-migration-20261026/`.
This is populated database integration evidence, not an installed package or
deployment upgrade; it does not close any channel handoff gate.

### Shared journal portability follow-up

A follow-up fingerprint-journal change exposed a portability regression: the
intermediate implementation required Bash, GNU `stat`, and `sha256sum`, which
are absent from the FreeBSD base used by the standalone archive. The journal is
now POSIX `/bin/sh` and selects the platform's native metadata and SHA-256
commands. The Linux fingerprint/desktop-supervisor regression passed, and a
FreeBSD 15.1-RELEASE-p6 guest passed source-directory fingerprinting, relocation
stability with a nested newline-containing filename, content-change refresh,
and symbolic-link rejection. Logs are retained in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/freebsd-journal-portability-0b0c38c8/`.
The AUR choice container, Debian Trixie bridge package/upgrade/downgrade, Fedora
44 RPM bridge transaction, 19-case legacy-channel contract suite, 9-case
runner-policy suite, and 98 focused database importer, multi-database
converter, and migration-journal tests also passed against the current
workspace.

The FreeBSD 0.9.978 x64 standalone archive was rebuilt with the portable
journal and tested in a FreeBSD 15.1-RELEASE-p6 guest. Its SHA-256 is
`36585cde8193bfa4641552fda72b368f7c63536196cec4c2a8fbaa2bd387ab77`; the
bundled journal passed the same fingerprint, relocation, refresh, symlink, and
state checks. This closes the exact-archive journal portability check. It
does not rerun the full app handoff against this rebuilt archive, and the exact
candidate archive check on macOS remains open. Evidence is retained in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/freebsd-standalone-portability-20261026/`.

### Unraid template Docker rehearsal

A local Docker rehearsal evaluated the legacy template contract against the
BookshelfNG 0.4.21.71 source image and a local ChaptarrNG 0.9.978 image. The
synthetic fixture's `/library` paths were mapped to the template's `/data`
mount, with four media files created at the database's `BookFiles.Path`
entries. Manual mode deferred without changing the source database or config.
Automatic mode migrated schema 158 to 124, passed `/ping` and SQLite
integrity/foreign-key checks, reported no missing media watch paths, preserved
the config settings and all four media files plus `/data`/`/download` canaries,
and retained a backup whose schema and rows matched all 39 source tables. A
forced health failure restored all source tables and rows plus the config byte
for byte, preserved the media files, then the BookshelfNG image returned
healthy at `/ping`. The target database contained 4 authors, 9
format-specific books, 9 editions, and 4 book files.

This tests the local image/container handoff and the settings/mount contract;
it does not test the Unraid Community Applications UI, an Unraid host update,
published image delivery, or an official package rollback. Those remain open.
Image IDs, the reproducible harness, checksums, and filtered milestones are in
`/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/unraid-template-docker-d43a005/`.

### Private Windows runner handoff

The Windows package and installer results above are historical hosted runs;
they establish package behavior but do not satisfy the current private-JIT
runner requirement. ChaptarrNG's local build, migration-installer, and
Chocolatey workflow jobs now target the historically approved private route
`[self-hosted, Windows, X64, packer-windows]`. The organization currently has no
online Windows registration carrying that label, so the private route and its
release jobs are still unverified. Keep those jobs queued until the
Packer/runner-policy owner restores the existing runner group/labels and
broker/workflow access and passes a Windows canary after C0 clearance.
Restore the approved route in place; do not move Windows
jobs to `windows-latest`, Linux JIT, or another runner. Keep them queued until
the original runner group/labels and broker/workflow access are restored and
the private Windows canary has passed.

At the 2026-10-26 14:08 UTC follow-up, runner group ID 11 still selected only
`snapetech-org/packer`; no runner carried `packer-windows`, and ChaptarrNG was
not selected. The API showed zero busy registrations, but BookshelfNG CodeQL
run `40332133668` still had three jobs queued. ChaptarrNG Release preflight
`40334519893` and Helm handoff `40334519631` were queued at main commit
`440a5644`. The C0 slot owner had not confirmed the drain, so the shared pool
and these runs were left untouched. The Release run is a potential publication
path for version `0.9.978` (latest published stable tag: `v0.9.977`); preserve
it and do not dispatch a duplicate or push a new head until its readiness and
side effects are reviewed.

The current organization-wide directive requires every build, test, security
scan, package build, and release workflow across all 65 organization
repositories to use approved private JIT capacity, with no hosted fallback.
The VM and hosted run records above remain behavioral evidence, but they must
be replayed through the approved private route before this campaign can be
called complete under the current execution policy.

At 2026-10-26 14:14 UTC the runner API still showed no Windows or public-fork
run-scoped registration, while the ChaptarrNG Windows and fork workflow edits
were local and untested. The Release preflight and Helm handoff remained
queued, and the C0 slot-owner clearance was still missing. No new CI or
publication run was started.

At 14:22 UTC, the live GitHub API showed that `ng-shared-main-jit` group ID 10
now selects all 65 organization repositories, including ChaptarrNG. No busy
runner registration or online ChaptarrNG Linux, Windows Packer, or public-fork
JIT runner was present in the runner API snapshot. Runner group ID 11 still
selects only `snapetech-org/packer`, so ChaptarrNG remains excluded from the
Windows route; group ID 5 selects seven repositories, including ChaptarrNG,
but has no registered fork runner. BookshelfNG CodeQL run `40332133668` still
has its Python, JavaScript/TypeScript, and Actions jobs queued. ChaptarrNG
Release preflight `40334519893` and Helm handoff `40334519631` remain queued at
main SHA `440a5644`. The older route-candidate run `40313259957` remains
incomplete: Docker and PostgreSQL jobs succeeded, Linux x64 failed, and Intel
macOS plus Windows x64 remain queued. This snapshot was read-only; the C0
handoff is still unconfirmed, and no queue, runner group, broker, or host was
changed.

- Debian/PPA and Fedora/COPR publisher paths remain closed; direct VM package acceptance does not prove a PPA or COPR feed upgrade. The public BookshelfNG COPR repository had no packages for the tested Fedora series.
- The Chocolatey legacy bridge handoff remains unresolved. A live Community Repository check on 2026-10-26 shows the public `bookshelfng` package at 0.4.21.76, whose install script still installs the BookshelfNG `Readarr.exe` archive, and a separate `chaptarrng` package at 0.9.973. Neither listing establishes publication or acceptance of the current 0.9.978 same-identity BookshelfNG replacement bridge. The local 0.9.978 package passed, but that bridge artifact has not been published. The recorded full Windows VM No/Yes run used predecessor 0.4.21.70; repeat the full handoff against current public 0.4.21.76 and the exact 0.9.978 bridge on approved private Windows JIT. The release-distribution workflow is configured to exercise the latest public BookshelfNG package's Yes path and a Manual defer, but it has not run on private Windows JIT. [BookshelfNG package](https://community.chocolatey.org/packages/bookshelfng) · [ChaptarrNG package](https://community.chocolatey.org/packages/chaptarrng)
  The current local `release-distribution.yml` change adds a real Chocolatey `MigrationMode=manual` update attempt before the automatic handoff and checks that the service, database, data canary, service executable, and staging/handoff paths remain unchanged. It also requires the package's exact manual-cancel diagnostic, so an unrelated nonzero Chocolatey failure cannot count as a successful defer. This is not an interactive No response; the separate PowerShell regression checks the prompt's No/Yes choices. The database hash is sampled while the service is running, so ordinary service writes could fail the check; the fresh package VM has no seeded user tasks or data. Neither change has run on private Windows JIT yet.
- The AUR update candidate is local and unpublished.

At 2026-10-26 14:32 UTC, the read-only GitHub Actions configuration check
returned no repository variables. The release workflows require
`CHAPTARRNG_MIGRATION_BRIDGE_ENABLED`, `CHAPTARRNG_RELEASE_READY`, and
`BOOKSHELFNG_RELEASE_READY`; the Launchpad publisher additionally requires
`LAUNCHPAD_PPA`, `LAUNCHPAD_SERIES`, and the `GPG_PRIVATE_KEY` secret. None of
those were configured in the observed repository state, so legacy publication
gates cannot be enabled from the current configuration.

- Docker alias/image delivery, Helm OCI chart delivery, AppImage/Flatpak publication, and external catalog updates remain gated.
- StartOS passed synthetic x86_64 and aarch64 VM handoffs, including Manual, Automatic, health, and automatic source rollback. Its official signed package, public `.71` predecessor update, external repository, and package-manager rollback remain open. Cloudron's local installed platform path passed, but candidate/feed publication remains open and the `.4.21` pre-update backup excludes persistent directories. Co-op Cloud passed local Swarm update, defer, failure-pause, full-volume restore, official Backupbot II CLI backup/restore, and Abra CLI backup/restore orchestration; canonical recipe review, published digest, and maintainer acceptance remain open. Unraid and external catalog platform handoffs still need their own update and rollback tests.
- The Windows standalone installer passed hosted Yes, No-at-preview defer, injected-failure rollback, explicit restore, service cleanup, and selected-data-directory checks. The old BookshelfNG updater cannot be redirected from this repository.
- macOS legacy installs need a one-time manual bootstrap; hosted x64 and arm64 runtime handoff acceptance now passes. The old BookshelfNG updater cannot be redirected from this repository. FreeBSD installed No/Yes/rollback acceptance is complete, but the one-time launcher/service path switch remains an operator action.
- Representative operator data and production provider/API workflows are outside this channel-package campaign.

Do not mark the global channel gate complete or publish these candidates until the blocked delivery and platform checks above are closed or the supported-channel scope is explicitly changed.

## Private JIT RPM failure diagnosis — 2026-10-26 14:47 UTC

Read-only inspection of the failed Linux x64 job in [run 40313259957](https://github.com/snapetech-org/chaptarrng/actions/runs/40313259957) confirmed that the runner was in the private `ng-shared-main-jit` group. The job reached the Fedora RPM bridge harness but failed before `rpmbuild` or the package transaction: `dnf install` could not fetch Fedora 44 updates metadata because the configured proxy returned HTTP 403 for the Fedora metalink. The Debian bridge and AUR choice steps had already passed in that job. This is a blocked dependency-source setup, not evidence of an RPM handoff regression or pass. A qualifying rerun needs an approved Fedora mirror or dependency cache reachable from private JIT; do not bypass the configured egress policy. The run remains incomplete and was not retried.

## Private JIT Helm handoff result — 2026-10-26 15:53 UTC

The existing Helm handoff run `40343577463` completed on the approved private `ng-shared-main-jit` group, using run-scoped labels `ng-shared-main-chaptarrng` and `ng-main-run-40343577463` on runner `gha-kspld0-40343577463-121644429659-809914`. Checkout, BookshelfNG history fetch, pinned-revision verification, Helm 3.20 setup, and chart lint passed. The run then failed in `ruby/setup-ruby` before `scripts/test_bookshelfng_helm_handoff.rb` could execute: the action received `EACCES: permission denied, mkdir '/opt/hostedtoolcache'`. The [official self-hosted runner requirements](https://github.com/ruby/setup-ruby#using-self-hosted-runners) require that default tool-cache path to be writable by the runner account. The chart behavior assertions therefore remain unverified on private JIT. Correct the ephemeral runner image/tool-cache permissions through the approved runner-owner handoff, then validate this job once; do not retry or change shared runner configuration before C0 clearance.

At the same recheck, ChaptarrNG Build `40343824614` and Release `40343577857` remained queued at `35889859fd96e2ffb2710ad95b84bd1d07034a03`; Packer PR #11 run `40351385087` remained queued at `852347d9cd246520b7163c2909589dfaa26a57a7`. No C0/slot-owner handoff was visible. Existing queue work and the Packer image build remain untouched.

## Private release gate and live queue recheck — 2026-10-26 16:10 UTC

Release run `40343577857` completed on private JIT and failed its release-readiness preflight because both `CHAPTARRNG_RELEASE_READY` and `BOOKSHELFNG_RELEASE_READY` were unset. Validation, package-build, release, and publish jobs were skipped; the run published nothing. Keep those gates closed until channel acceptance is complete.

The exact-head ChaptarrNG Build run `40343824614` remains queued at `35889859fd96e2ffb2710ad95b84bd1d07034a03`: Linux x64, Windows x64, Intel macOS, PostgreSQL migrations, and all three Docker matrix jobs are still queued. Its release-note contract job was skipped. BookshelfNG release-note run `40349225353` remains queued at `a41e25f92c6c24940973446faf6f671d427100e7`; trusted Packer PR #11 dispatch run `40351385087` remains queued at `852347d9cd246520b7163c2909589dfaa26a57a7`. No owner-thread update or C0/slot-owner handoff was present at this recheck. Preserve all queued work; no new CI or publication run was started.

## Channel and private JIT recheck — 2026-10-26 16:56 UTC

The current public Chocolatey predecessor is approved BookshelfNG `0.4.21.76`; its package still installs the BookshelfNG `Readarr.exe` archive into the BookshelfNG application and data paths. The separate public ChaptarrNG package remains `0.9.973`. Neither listing is the unpublished `0.9.978` same-identity BookshelfNG bridge. The recorded full Chocolatey VM handoff used predecessor `0.4.21.70`; testing `.76` against the exact bridge on private Windows JIT remains open. The [public BookshelfNG package listing](https://community.chocolatey.org/packages/bookshelfng) was updated October 26, 2026.

The local Chocolatey `MigrationMode=manual` step now rejects unrelated command failures by requiring the package's exact cancellation diagnostic before it accepts the unchanged service, database, executable path, canary, and no-staging checks. This workflow edit is uncommitted and has not run on private Windows JIT. The separate prompt regression has not run on private Windows JIT either.

At 16:56 UTC, GitHub still showed ChaptarrNG Build run [40343824614](https://github.com/snapetech-org/chaptarrng/actions/runs/40343824614) queued across its Linux, Windows, macOS, Docker, and PostgreSQL jobs. BookshelfNG release-note run [40349225353](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225353) remained queued. Release run [40343577857](https://github.com/snapetech-org/chaptarrng/actions/runs/40343577857) had failed because `CHAPTARRNG_RELEASE_READY` and `BOOKSHELFNG_RELEASE_READY` were empty; all publication jobs were skipped, and nothing was published. Helm run [40343577463](https://github.com/snapetech-org/chaptarrng/actions/runs/40343577463) failed at `ruby/setup-ruby` because the runner could not create `/opt/hostedtoolcache`, before the chart acceptance script ran.

Packer PR #11 merged at `881185c7587b776af3953237aafdd8e6502919ff` at 16:44 UTC, but its private-JIT dispatcher run `40351385087` remains queued. The merge is not a private-JIT validation or broker-deployment result. Packer PR #9 remains open and draft without a C0/slot-owner drain handoff. Its latest owner-PR run `40362638778` has a successful Packer validation job but its trusted-owner report job remains queued; the Windows channel canary has not passed. The 16:54 UTC operational snapshot reported two JIT guests active or starting; preserve both and leave the broker running.

Release readiness and migration-bridge gates remain closed. StartOS signed-package, repository and rollback acceptance; Cloudron and Unraid delivery; the canonical Co-op Cloud recipe; package-feed and catalog handoffs; and the remaining private-JIT replays are still open. Do not mark global channel acceptance complete.

The worktree is based on ChaptarrNG `main` at `ea225f3aa90a3a1216408cac536f5499993c589b`, which requires private-JIT validation without hosted fallback. The four task-file edits are still uncommitted and have not run on private JIT.

## Shared JIT queue update — 2026-10-26 17:02 UTC

BookshelfNG main CI run [40349225827](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225827) has a private-JIT job running on `kspls0`; its Chocolatey verifier `121664388901` and release-note, CodeQL, Semgrep, and Trivy jobs remain queued. This is not the ChaptarrNG private Windows `.76` bridge handoff, which still has no passing run.

The broker snapshot at 17:00:48 UTC reported four active guest reservations, 18 eligible jobs, zero free slots, and zero hosted recoveries; the unchanged memory reserve is blocking admission. Preserve the active jobs and queue. The Packer PR #11 cache fix is merged, but its exact private-JIT run `40351385087`, current-main validation `40364071163`, and PR owner validation `40364482118` remain queued. No broker deployment or Chocolatey `.76` handoff is established.

## BookshelfNG release-build policy failure and guard — 2026-10-26 17:09 UTC

BookshelfNG CI run [40349225827](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225827) completed with its `build` job failed in `Test private JIT runner policy`. The private-JIT job ran 12 policy tests and reported four failures: `validate`, `build-platform-runtime-packs`, `build-release-assets`, and `build-linux-packages` did not each include the required `inputs.tag != ''` condition. Release build and publication jobs were skipped. At the 17:09 GitHub API recheck, the same run's Chocolatey verifier `121664388901` and release-note run [40349225353](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225353) were still queued.

The correction is pushed to BookshelfNG branch `codex/release-build-empty-tag-guard-20261026` at `91d0115210579f403c82d600cb2047d22df40429`. It adds the empty-tag guard to all four release-build jobs. The branch has no status checks and has not been validated on private JIT; no PR or workflow dispatch was created. The four ChaptarrNG task-file changes were previously pushed to `codex/channel-acceptance-20261026` at `a1ecd427c419569b72679ac433b0bbb6550b5738`; that commit has no status checks or private-JIT validation. This 17:09 report refresh is being pushed as a follow-up commit. No tests, scans, publications, or shared-runner changes were started by this update.

## Final queue snapshot — 2026-10-26 17:11 UTC

The latest read-only GitHub status check still shows BookshelfNG Chocolatey verifier `121664388901` and release-note run [40349225353](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225353) queued. ChaptarrNG Build [40343824614](https://github.com/snapetech-org/chaptarrng/actions/runs/40343824614) still has all seven platform/migration jobs queued; Release [40343577857](https://github.com/snapetech-org/chaptarrng/actions/runs/40343577857) failed its readiness preflight with every release/publish job skipped; Helm [40343577463](https://github.com/snapetech-org/chaptarrng/actions/runs/40343577463) failed on the private runner's `/opt/hostedtoolcache` permissions before the acceptance script.

Packer PR #11 dispatcher run [40351385087](https://github.com/snapetech-org/packer/actions/runs/40351385087), current-main validation [40364071163](https://github.com/snapetech-org/packer/actions/runs/40364071163), and owner validation [40364482118](https://github.com/snapetech-org/packer/actions/runs/40364482118) remain queued. The cache fix is merged but is not validated or deployed. No C0/slot-owner handoff was available, so no run was dispatched or retried. The 65-repository/407-workflow audit was not repeated; private-route coverage remains incomplete. Neither task branch has private-JIT validation, and no tests, scans, publications, or shared-runner changes were started by the final recheck.

## Private-JIT queue monitor — 2026-10-26 17:24 UTC

ChaptarrNG Build [40343824614](https://github.com/snapetech-org/chaptarrng/actions/runs/40343824614) completed private-JIT job `postgres-migrations` (`121645321563`) successfully, including its build and fresh PostgreSQL migration smoke. Its other six platform and image jobs remain queued.

BookshelfNG PR [#213](https://github.com/snapetech-org/bookshelfng/pull/213), head `f8c06db28e1a314b0313e20d8460c49f10e1b19e`, is the active fix for the BookshelfNG policy-test failure. It tests the existing tag/readiness/ancestry guards and dependency chain instead of changing release workflow behavior, and fixes the fork workflow YAML/pin. Its trusted-owner dispatcher, CodeQL, Semgrep, Trivy, package-sync, and dependency-review jobs remain queued. My separate branch `codex/release-build-empty-tag-guard-20261026` adds workflow conditions and is an unvalidated alternative; PR #213 supersedes it, so do not merge that branch.

Packer main validation run [40364071163](https://github.com/snapetech-org/packer/actions/runs/40364071163) completed successfully, including shared-runner broker policy tests, ShellCheck, unattended XML validation, and Packer format. Packer PR #11 dispatcher [40351385087](https://github.com/snapetech-org/packer/actions/runs/40351385087) and owner validation [40364482118](https://github.com/snapetech-org/packer/actions/runs/40364482118) remain queued. The 17:17:46 broker snapshot reported three active guests and no spare slots; no newer host capacity or broker deployment readback was made. No C0/slot-owner handoff was found. No job was dispatched, canceled, or retried during monitoring.

## Release-note failure and queue recheck — 2026-10-26 17:27 UTC

BookshelfNG release-note run [40349225353](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225353) has now failed. Its diagnostic identifies missing required front matter in `release-notes/2026-10-26-public-fork-validation-jit.md`: `audience`, `area`, `action`, and `breaking`. The same check for PR [#213](https://github.com/snapetech-org/bookshelfng/pull/213) remains queued, as do that PR's trusted-owner dispatcher, CodeQL, Semgrep, Trivy, package-sync, and dependency-review jobs. I left the PR head unchanged while those checks are queued; its release-note fragment will need valid metadata before it can pass.

The BookshelfNG Chocolatey verifier `121664388901` remains queued. Packer main validation run `40364071163` and owner project validation `40364482118/121718395113` passed, but the Packer PR #11 dispatcher `40351385087/121672057828` and owner report `40364482118/121732498345` remain queued. ChaptarrNG's PostgreSQL migrations job passed; its other six Build jobs remain queued. No newer broker capacity readback or C0/slot-owner handoff is available. No job was dispatched, canceled, or retried.

## Packer owner-validation result — 2026-10-26 17:29 UTC

Packer owner-validation run [40364482118](https://github.com/snapetech-org/packer/actions/runs/40364482118) has now completed successfully: project validation job `121718395113` and its owner-validation report `121732498345` both passed. This does not clear the separate PR #11 dispatcher job `40351385087/121672057828`, which remains queued, or establish a broker deployment. The ChaptarrNG migration smoke is still the only completed job in its Build run; the other six remain queued. BookshelfNG PR #213 checks and the Chocolatey verifier remain queued, and the malformed release-note metadata failure is unresolved.

## Channel and private-JIT recheck — 2026-10-26 17:41 UTC

BookshelfNG `v0.4.21.76` is the final published GitHub release, with 38 assets; tag CI `40252457919` passed. The follow-on channel workflow `40253426242` failed at `gh release upload` with `fatal: not a git repository`, so it did not publish package channels or announce the release. AUR still lists `bookshelfng-bin` `0.4.21.71-1`; GHCR has the `.76` Softcover and Hardcover tags. The existing release-publication workflow is hosted-first and cannot be retried under the private-JIT-only policy. Do not run its manual retry until channel state and duplicate side effects are checked. No new stable tag is authorized while the release freeze remains in place.

The private Chocolatey `.76` handoff is still open. The local manual-defer verifier now requires the exact package cancellation diagnostic and compares stable fingerprints for `readarr.db`, its WAL, and rollback journal; the canary contents; service PID, identity, and start mode; and NSSM application, parameters, and directory. This is a source-level improvement only: it has not run on private Windows JIT, and the `.70` VM No/Yes rehearsal does not accept `.76`.

ChaptarrNG Build [40343824614](https://github.com/snapetech-org/chaptarrng/actions/runs/40343824614) has PostgreSQL migration job `121645321563` passed; its six platform/image jobs remain queued. Older archive run [40140157996](https://github.com/snapetech-org/chaptarrng/actions/runs/40140157996) is still in progress with `linux-x86` job `121248859010` failed during the second artifact-download step and `osx-arm64` job `121248859037` failed while building the archive; seven archive jobs remain queued. GitHub has not exposed logs while the run is still in progress, so the failure causes are not established. No retry was started.

BookshelfNG main run [40349225827](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225827) still has its policy-test build failed and Chocolatey verifier `121664388901` queued. Semgrep run [40349246909](https://github.com/snapetech-org/bookshelfng/actions/runs/40349246909) passed. Release-note run [40349225353](https://github.com/snapetech-org/bookshelfng/actions/runs/40349225353) failed for missing `audience`, `area`, `action`, and `breaking` front matter. PR [#213](https://github.com/snapetech-org/bookshelfng/pull/213) remains at its existing head; its trusted-owner dispatcher `40366867930/121726843703` and release-note check `40366873759/121726858961` are queued.

Packer main validation [40365850712](https://github.com/snapetech-org/packer/actions/runs/40365850712) passed. Packer PR #9 run `40365815504` dispatched successfully but skipped its downstream validation and report; an earlier owner-validation run `40359521085/121717299050` passed. This is not a C0/slot-owner handoff. PR #11 dispatcher `40351385087/121672057828` remains queued; no broker deployment is established.

At 17:40:31 UTC the broker reported four active jobs, 31 eligible, and zero hosted recoveries. It had automatically dispatched Packer owner job `40370450408/121739449473` to kspld0 and ChaptarrNG archive job `40140157996/121248858975` to kspls0. The unchanged disk and memory admission guards remain active. No C0/slot-owner handoff arrived; preserve active guests and queued work, and do not restart or reconfigure the broker.
