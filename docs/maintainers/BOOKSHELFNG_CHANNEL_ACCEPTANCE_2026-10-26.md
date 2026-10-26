# BookshelfNG channel acceptance — 2026-10-26

## Result

The local 0.9.978 candidate passed installed handoff acceptance on Debian, Fedora, Arch, Windows Chocolatey, FreeBSD standalone, Docker, Helm, AppImage, Flatpak, and Co-op Cloud Swarm paths described below. The shared standalone launcher also passed a Linux runtime harness covering No, headless Manual, Yes, health, source backup, and forced rollback. Cloudron 10.1.6 passed an installed `.4.21` to `.4.23` package update, Manual defer, native backup/restore, Automatic migration, and the source-database plus old-package rollback path. StartOS passed x86_64 and aarch64 test-package updates covering Manual defer, automatic migration, health checks, and automatic source rollback; these remain synthetic, test-key rehearsals. A local Unraid-template Docker rehearsal passed manual defer, automatic migration, health, and forced rollback; it does not count as Community Applications or host acceptance. The optional choice is presented at the package prompt, a terminal-backed desktop or standalone launch, or an explicit platform setting, depending on the channel. pacman uses a deferred two-transaction handoff because pacman scriptlets cannot cancel an active transaction.

This is **not 100% release-channel acceptance**. No replacement artifact was published, and several package feeds contain no BookshelfNG predecessor. StartOS still needs the official signed package, external repository delivery, and package-manager rollback acceptance. Cloudron feed/catalog publication, Unraid Community Applications/host update and rollback acceptance, canonical Co-op Cloud recipe acceptance, and external catalog installations remain open. The old Cloudron `.4.21` package also cannot back up its persistent directories; that legacy backup gap is covered only after the new package with backup hooks is installed. Release gates remain closed.

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
| StartOS | No y/n prompt; package configuration uses an automatic/manual choice | Set Manual through the package action and start the candidate; conversion is deferred, the app remains unavailable, and the schema-158 source stays unchanged. Run the documented Console preview/apply, then switch back to Automatic and restart. | Set Automatic and start the candidate; the replacement previews, converts, retains its source backup, passes database checks, and serves `/ping`. A failed health check restores the source database and leaves ChaptarrNG stopped. | StartOS 0.4.0.3 x86_64 and aarch64 VMs passed test-key SDK3 package updates. ARM64 used synthetic predecessor `.4.21:3` to candidate `.9.979:1`; Manual defer, Console preview/apply, Automatic conversion, and injected health-failure rollback all passed with `/books` and `/downloads` canaries. The predecessor fixture wraps the pinned BookshelfNG Readarr 0.4.21.37 ARM64 image with a direct service command because its s6 `/init` requires PID 1. Official signed `.s9pk`, public `.71` package, external repository, and package-manager rollback remain unaccepted. |
| Unraid | No y/n prompt in the template update flow | Set migration mode to Manual in the template before applying the image update; conversion is deferred until Automatic is selected and the container is restarted. | Set Automatic and restart; the image performs migration on startup. | Local Docker rehearsal against BookshelfNG 0.4.21.71 and a local ChaptarrNG 0.9.978 image passed manual defer, migration, `/ping`, retained source data, settings and media canaries, and forced rollback/recovery using the legacy template's mount and environment contract. This was not run in Unraid Community Applications or on an Unraid host; image publication remains open. |
| Cloudron | No terminal prompt; the administrator can hold the app update or set manual/automatic mode | Set `BOOKSHELFNG_MIGRATION_MODE=manual` before applying the package update; the proxy keeps `/ping` healthy and shows recovery instructions while the database remains unchanged. | Apply the update with automatic mode to convert on startup; Cloudron custom app environment can override the image default. | Cloudron 10.1.6 installed `.4.21` to `.4.23`; Manual defer, native backup/restore, Automatic migration, and source-database plus old-package rollback passed. The `.4.21` pre-update backup does not include persistent directories; `.4.23` adds backup/restore hooks. Feed/catalog publication remains open. |
| Co-op Cloud | No interactive y/n in Swarm. To choose No, keep the BookshelfNG stack deployed. Deploying the candidate with `manual` defers conversion but the new service does not serve `/ping` until the operator switches to Automatic and redeploys. | A Manual candidate update keeps the schema-158 database byte-identical and records `deferred`; the app endpoint is unavailable while held. | Redeploy the same stack in Automatic mode; the compatibility image converts, validates, retains a source backup, and serves `/ping`. | Isolated Debian 13 / Docker 26.1 Swarm upgraded the pinned BookshelfNG .71 source image to the exact-head .978 candidate. `bookshelfng_app` and all three volume names stayed stable. Manual preserved the database SHA-256 `f126ce2ae615dd6e898a446a2b122bc90ae9d10ea9a9ab2090cf0ca90830a1a1`, schema 158, and media canaries. Automatic reached schema 124, passed SQLite checks, kept the source backup and canaries, and served `/ping`. An injected unhealthy update paused as configured; restoring the three-volume snapshot and redeploying .71 returned healthy BookshelfNG at schema 158. The official Backupbot II 2.4.0 image (`sha256:870277cdba4b91ad6771184a7e208ba29151b951e39c68b4a3fd6f8d35252feb`) backed up those same volume identities using the recipe label. Its restore recovered a corrupted schema-158 database byte-for-byte, passed SQLite checks, restored all three volume canaries, and left the service mounts unchanged. Abra 0.13.0-beta deployed an isolated recipe with the same service and volume identities, created and listed the snapshot, then restored the corrupted database byte-for-byte and restored all three canaries after the app was scaled to zero. The app restarted with unchanged mounts. Canonical recipe acceptance remains open. |
| Self-hosting catalogs | No common y/n contract | Depends on each catalog's UI and image replacement behavior. | Compose candidate preserves the bookshelfng service and volume identities. | Local Compose contract only; CasaOS, ZimaOS, Umbrel, TrueNAS, Cosmos, CapRover, Portainer, and other external listings were not updated or accepted. |
| macOS standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No or set `BOOKSHELFNG_MIGRATION_MODE=manual`; keep the prior app and service path. | Answer Yes or set `automatic`; the wrapper stages migration, starts ChaptarrNG, and checks health. The operator switches the launcher/service after acceptance. | Hosted run [40270599787](https://github.com/snapetech-org/chaptarrng/actions/runs/40270599787) passed on macOS x64 and arm64 against checksum-pinned public .71 predecessors: No, headless Manual, Yes, health, source backup, and forced rollback. It publishes no assets and validates the handoff runtime; the one-time app/service path switch remains manual. |
| FreeBSD standalone installs | No BookshelfNG-owned update channel to redirect; the bundled launcher asks when run in a terminal. | Answer No; the candidate exits without changing predecessor config/data. Restart BookshelfNG if it was stopped for the handoff. | Answer Yes; migration completes, ChaptarrNG starts, and health passes. The operator switches the launcher/service after acceptance. | FreeBSD 15.1 VM: installed BookshelfNG 0.4.21.71 to the exact local 0.9.978 archive; No, Yes, health, source backup, and forced-failure rollback passed. |
| Windows standalone migration installer | Yes, through its explicit Migrate BookshelfNG action and the migration preview's Yes/No confirmation; closing the window defers | Close without selecting Migrate, or answer No at preview; BookshelfNG remains installed. | Select Migrate, review the preview, then answer Yes to run backup, conversion, service handoff, and health checks. | Hosted Windows run [40218131519](https://github.com/snapetech-org/chaptarrng/actions/runs/40218131519) installed the checksum-pinned .71 service, forced automatic rollback, completed the real installer handoff, and exercised explicit restore. The installer source is unchanged since that run. Hosted Windows run [40274632625](https://github.com/snapetech-org/chaptarrng/actions/runs/40274632625) completed the No-at-preview service round-trip; No kept BookshelfNG installed, Yes completed the upgrade, and injected-failure rollback, explicit restore, service cleanup, and selected-data-directory checks passed. Chocolatey remains a separate channel. |

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

The shared Linux standalone handoff harness used a checksum-verified public BookshelfNG .71 predecessor (SHA-256 `4197d53b757ebe99642fb8f4213f963dd54641401d1f188ab6daadcb3b024d5d`) and the exact ChaptarrNG candidate archive (SHA-256 `359897785599f850c2264d2cc2aad96c1fa1e69ffd4ad1b6e98e3d783c75d364`). No preserved the predecessor and restarted its `/ping`; headless Manual deferred without changing source files; Yes migrated data to schema 124, passed integrity and foreign-key checks, retained a logical source backup, and returned healthy `/ping`. An injected replacement-health failure restored the source database and restarted BookshelfNG while retaining the failed target for diagnosis. The rerun also confirms the harness now closes the PTY after a stalled Ctrl+C shutdown and reaps the candidate. This Linux harness validates the shared launcher/supervisor; Linux standalone archives do not gain a new update channel from it. Full output: `/var/tmp/chaptarrng-channel-acceptance-20261026/evidence/standalone-handoff-stop-fix-v6.log`.

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

This is code-level evidence for the modified worktree at `d43a005`; it does not
replace the installed-package evidence above, which used an earlier `.978`
candidate. No package was rebuilt and installed from the current diff across
the platform VMs.

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

The private-only workflow edits remain local and unpushed. No CI or canary was
started. These local checks do not close the signed-package, feed/catalog,
Unraid, canonical Co-op Cloud, external catalog, or private JIT rollout gates.

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

The Windows package and installer acceptance evidence above includes successful
hosted Windows runs. ChaptarrNG's local build, migration-installer, and
Chocolatey workflow jobs now target the historically approved private route
`[self-hosted, Windows, X64, packer-windows]`. The organization currently has no
online Windows registration carrying that label, so the private route and its
release jobs are still unverified. Keep those jobs queued until the
Packer/runner-policy owner restores the existing runner group/labels and
broker/workflow access and passes a Windows canary after C0 clearance. The
hosted VM results establish package behavior; they do not establish private
runner acceptance. Restore the approved route in place; do not move Windows
jobs to `windows-latest`, Linux JIT, or another runner. Keep them queued until
the original runner group/labels, broker/workflow access, and canary are
verified.

- Debian/PPA and Fedora/COPR publisher paths remain closed; direct VM package acceptance does not prove a PPA or COPR feed upgrade. The public BookshelfNG COPR repository had no packages for the tested Fedora series.
- The Chocolatey feed rejection is unresolved. The local package passed, but no public package was pushed.
- The AUR update candidate is local and unpublished.
- Docker alias/image delivery, Helm OCI chart delivery, AppImage/Flatpak publication, and external catalog updates remain gated.
- StartOS passed synthetic x86_64 and aarch64 VM handoffs, including Manual, Automatic, health, and automatic source rollback. Its official signed package, public `.71` predecessor update, external repository, and package-manager rollback remain open. Cloudron's local installed platform path passed, but candidate/feed publication remains open and the `.4.21` pre-update backup excludes persistent directories. Co-op Cloud passed local Swarm update, defer, failure-pause, full-volume restore, official Backupbot II CLI backup/restore, and Abra CLI backup/restore orchestration; canonical recipe review, published digest, and maintainer acceptance remain open. Unraid and external catalog platform handoffs still need their own update and rollback tests.
- The Windows standalone installer passed hosted Yes, No-at-preview defer, injected-failure rollback, explicit restore, service cleanup, and selected-data-directory checks. The old BookshelfNG updater cannot be redirected from this repository.
- macOS legacy installs need a one-time manual bootstrap; hosted x64 and arm64 runtime handoff acceptance now passes. The old BookshelfNG updater cannot be redirected from this repository. FreeBSD installed No/Yes/rollback acceptance is complete, but the one-time launcher/service path switch remains an operator action.
- Representative operator data and production provider/API workflows are outside this channel-package campaign.

Do not mark the global channel gate complete or publish these candidates until the blocked delivery and platform checks above are closed or the supported-channel scope is explicitly changed.
