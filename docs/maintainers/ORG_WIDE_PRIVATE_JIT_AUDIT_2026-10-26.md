# Organization-wide private JIT runner audit — 2026-10-26

## Status

The user’s current scope is all 65 `snapetech-org` repositories. Every build must use private, platform-appropriate JIT capacity, with no GitHub-hosted fallback. The inventory is complete; rollout and acceptance are not. This audit did not dispatch jobs or change shared runner or broker configuration. Packer PR #8 and PR #5 have merged; their source changes do not by themselves prove live group or runner cutover.

## Live handoff check — 2026-10-26

Packer PR [#5](https://github.com/snapetech-org/packer/pull/5), which expands the shared private JIT group across all 65 repositories, merged at 2026-10-26 11:28 UTC as Packer main commit `b48486553adaaed7d177d82abea6181fcfaf44ae`. Exact-head validation [40314650653](https://github.com/snapetech-org/packer/actions/runs/40314650653) succeeded on private Packer runners, but the merged source does not itself apply the live group cutover. The live `ng-shared-main-jit` group API still selects 12 repositories: `bookshelfng`, `chaptarrng`, `iptvtunerr`, `packer`, `PlezyNG`, `plezyng.app`, `QuestarrNG`, `ROMarrNG`, `seerrng`, `slskdN`, `slskr`, and `TorrentNG`. This does not match the 65-repository target. The merged policy source expects 65 repositories and 89 exact workflow refs; the live group remains at 12 repositories and 88 refs. The inventory reports 88 matched workflow files and 319 of 407 outside the main route map, covering 503 jobs, with additional gaps inside matched workflows. The GitHub runner-group workflow-list endpoint returned 404 during this read-only check, so the 88 live refs were not independently enumerated here.

At 2026-10-26 11:46 UTC, the organization runner API reported five online registrations: three idle and two busy Linux JIT guests on `kspld0`, both labeled for run `40313259957`. No online registration carried the Windows `packer-windows` label. The persistent Apple Silicon runner is not JIT capacity. The busy registrations mean the required zero-active guest/registration handoff is not clear; no shared cutover was performed.

The ChaptarrNG main checkout and `origin/main` are both at `e9900d8d120e2a952c12ba45af1eb8394fa8bf34`. The private-only route changes are already in main (`ca676141` and `7cbb1d2e`); Windows jobs request `[self-hosted, Windows, X64, packer-windows]`. The local workflow policy suite passed 10/10. GitHub currently reports run [40313259957](https://github.com/snapetech-org/chaptarrng/actions/runs/40313259957) as a queued `workflow_dispatch` at route commit `ca676141cbf3813268c3cbd07db9e195eb86a647`; two run-specific Linux JIT registrations are online and busy for it. This audit did not dispatch or alter that run, and it is not a completed canary.

Do not start another Actions job or cut over the shared group until the current run and registrations have drained, the slot owner confirms the handoff, and broker and group scopes are aligned. The 65/89 versus 12/88 gap, absent Windows registration, and remaining private-route gaps remain release blockers.

## Inventory

I enumerated the organization repositories and read the default-branch workflow directory for each. The scan found 65 repositories, 60 with workflow YAML, 407 workflow files, and no fetch errors. Five repositories have no workflow directory: `LGPrivacyShield`, `rustTorrent`, `seerrng-companion`, `snape.tech`, and `suniistuff`.

The per-job snapshot added by Packer [PR #8](https://github.com/snapetech-org/packer/pull/8), merged as `651863834624d10341b0871f23cffc509d96fd23`, records 779 jobs across those 60 repositories. It reports 667 jobs with at least one hosted runner label, 239 jobs using a `self-hosted` label, and 249 dynamic runner expressions. These sets overlap. The hosted-label jobs break down into 456 literal hosted targets, 205 expressions with hosted fallbacks, and 6 matrix jobs whose platform values include hosted labels. The snapshot also records matrix platforms and reusable-workflow calls in [the merged inventory CSV](https://github.com/snapetech-org/packer/blob/main/docs/org-workflow-inventory-20261026.csv).

ChaptarrNG has 17 workflow files and 48 job definitions in the Packer snapshot. That baseline recorded 42 jobs with hosted labels: 29 literal hosted targets and 13 conditional private-JIT routes that retain hosted fallbacks. Four jobs call reusable workflows. The current main branch removes hosted targets and hosted fallbacks from every ChaptarrNG workflow job: Linux x64 requests `ng-shared-main-jit`, Windows requests the historical `packer-windows` labels, unsupported macOS and Ubuntu 22.04 work requests private-only platform labels, and fork checks request the isolated `public-fork-jit` group. These are merged source routes, not runner acceptance. The Linux group currently authorizes only seven ChaptarrNG workflow paths; other release and package workflow paths are blocked pending coordinated access changes. Windows, macOS, Ubuntu 22.04, and public-fork capacity is unavailable or unverified. The other 64 repositories also remain open.

The inventory observes `windows-latest`, macOS labels, Linux ARM64 labels, `ubuntu-latest`, `ubuntu-24.04`, `ubuntu-22.04`, `ubuntu-slim`, and `ubuntu-24.04-arm`. The Packer snapshot’s per-job CSV is the source for repository, workflow, job, runner expression, platform matrix, and reusable-workflow details.

## Runner state and blockers

Packer PR #8 merged on 2026-10-26 at `651863834624d10341b0871f23cffc509d96fd23`. Its automatic main-push run [40304794881](https://github.com/snapetech-org/packer/actions/runs/40304794881) succeeded on the Linux JIT group `ng-shared-main-jit` with per-run labels. This verifies that Packer main validation used that Linux route; it does not verify deployment of the complete 65-repository broker scope, all repository workflow permissions, or the Windows route. The merged pagination fix is source evidence, not proof of live App installation or broker configuration.

The read-only GitHub group inventory shows `chaptarrng` is selected in both `ng-shared-main-jit` and `public-fork-jit`. The broker's current trusted-main catalog includes only `build.yml`, `codeql.yml`, `semgrep-community.yml`, `helm-handoff.yml`, `security.yml`, `standalone-asset-validation.yml`, and `trivy-filesystem.yml` for ChaptarrNG. Release, distribution, Docker publish, Unraid sync, announcement, and macOS acceptance workflows still need owner-reviewed exact workflow/job entries before those private jobs can run. The public-fork group has no registered runner. Run `40313259957` is queued and does not establish a successful ChaptarrNG canary.

| ChaptarrNG workflow | Private request | Current capacity/access gap |
| --- | --- | --- |
| `.github/workflows/build.yml` Linux x64, CodeQL, Semgrep, security, Helm, standalone validation, and Trivy | `ng-shared-main-jit`, Linux x64, per-run label | These seven paths are in the current broker catalog. A ChaptarrNG canary is still required. Tag-triggered `build.yml` runs are not enabled in the current broker entry. |
| `.github/workflows/release.yml`, `.github/workflows/release-distribution.yml`, `.github/workflows/docker-publish.yml`, `.github/workflows/discord-announcement.yml`, `.github/workflows/sync-unraid-legacy-ghcr.yml` | `ng-shared-main-jit`, Linux x64, per-run label | Workflow refs/job bootstrap entries are not in the current broker catalog; keep the jobs blocked until the runner-policy owner adds and verifies their exact scopes. |
| `.github/workflows/build.yml`, `.github/workflows/release.yml`, `.github/workflows/release-distribution.yml` Windows jobs | `[self-hosted, Windows, X64, packer-windows]` | No online registration currently carries `packer-windows`; the owner must restore the existing Windows runner/group path and pass a canary. |
| `.github/workflows/build.yml` macOS leg and `.github/workflows/bookshelfng-standalone-macos-acceptance.yml` | Private macOS x64/ARM64 labels | No macOS JIT capacity is verified. The existing Apple Silicon registration is persistent and is not used by these jobs. |
| `.github/workflows/release.yml` `platform-runtime-packs` and `.github/workflows/standalone-asset-validation.yml` `extra-platform-runtimes` | Linux x64 JIT plus `ng-jit-linux-22-04` | No Ubuntu 22.04 JIT image/label is registered. These jobs remain blocked. |
| `.github/workflows/public-fork-pr.yml`, `.github/workflows/fork-safe-validation.yml`, `.github/workflows/dependency-review.yml` | Isolated `public-fork-jit` group, Linux x64 | The group includes ChaptarrNG but has no registered runner; public-fork workflow admission still needs a private canary. Fork jobs have no hosted or trusted-runner fallback. |

The PR’s rollout note records these platform gaps:

- Linux x64 has a private JIT route, but the broker and runner-group workflow scope need the coordinated cutover.
- **Windows route correction:** the user says the private Windows Packer runner was not to be retired. Its approved historical workflow route is `[self-hosted, Windows, X64, packer-windows]`, recorded in the prior [private Windows workflow](https://github.com/snapetech-org/iptvtunerr/blob/4e695dbed53413a8b9de5858da93dba925c9dcab/.github/workflows/windows-smoke.yml). The current organization runner listing has no online Windows runner carrying `packer-windows`; do not treat the historical label contract as a live route. The Packer/runner-policy owner must restore the existing runner group/labels and broker/workflow access, then pass a Windows canary after coordinating the shared change. Keep dependent Windows jobs queued until that route is verified. ChaptarrNG’s local Windows workflow mappings now target those exact labels. The retired Packer dispatcher was configured for eight repositories; the 2026-10-26 inventory baseline contains 38 Windows-labeled job rows across 16 repositories, including an example workflow. The owner must review exact trusted workflow scope and preserve fork isolation before restoring a broader dispatcher.
- `private-apple-builds` is a persistent Apple Silicon runner, not a JIT runner.
- No verified private JIT route is available for Linux ARM64, Windows ARM64, Intel macOS, Apple Silicon JIT, or Ubuntu 22.04. ChaptarrNG's local Mac and Ubuntu 22.04 jobs now request private-only labels and remain blocked until that capacity and workflow access exist.
- PlezyNG’s WebOS lane needs a controlled local SDK and toolchain source.
- Public-fork jobs require a separate no-secret JIT route. They must remain isolated from trusted-main credentials and persistent hosts.

The merged Packer main includes a platform restoration assessment for [Windows](https://github.com/snapetech-org/packer/blob/main/docs/windows-runner-repo-assessment.md) and the broader [65-repository rollout plan](https://github.com/snapetech-org/packer/blob/main/docs/org-wide-private-jit-rollout.md). Those pages are planning evidence, not proof of deployed runner capacity.

The C0/current-slot-owner handoff is unresolved. As of 11:46 UTC, two run-specific registrations are online and busy, so the zero-active-guest/registration check fails. Do not start another CI job or change the shared broker, runner-group membership, or host configuration until the owner confirms the slot is clear and the check passes. The build matrix renamed its logical platform values to `linux-x64`, `windows-x64`, and `macos-intel`; the Packer broker's `build (ubuntu-latest)` bootstrap entry must be updated to the new Linux job name and retain its Docker/Python package setup before a canary.

## Package-channel acceptance remains open

The separate [BookshelfNG channel acceptance report](BOOKSHELFNG_CHANNEL_ACCEPTANCE_2026-10-26.md) records successful local installed rehearsals across the tested package channels. It also explicitly leaves release acceptance below 100%: replacement artifacts remain unpublished; StartOS still needs the official signed package, external repository delivery, and package-manager rollback acceptance; Cloudron feed/catalog publication remains open; Unraid installed lifecycle acceptance remains open; canonical Co-op Cloud recipe acceptance remains open; and external catalog installations remain open.

## Completion sequence

1. Obtain the C0 slot-owner handoff and verify zero active JIT guests and busy runner registrations.
2. Confirm the merged Packer pagination fix is deployed and validate the exact App installation and runner-group repository/workflow scope.
3. Have the Packer/runner-policy owner restore the existing Windows Packer JIT route (`packer-windows`), broker/workflow access, and a Windows canary; then convert Apple Silicon to one-job JIT and provision the missing native platform lanes.
4. Route every in-scope job to an exact private JIT group and per-run label. Remove hosted targets and fallbacks while preserving each workflow’s event gates, credentials boundaries, release triggers, and artifacts.
5. Update workflow policy checks so they reject hosted labels and missing JIT routes.
6. After the handoff, run a private JIT canary on each platform, then validate exact-head build, security, package, release, and artifact paths. Confirm the actual runner and artifact results, not queue movement alone.
7. Complete the outstanding package-channel publication and maintainer handoffs in the channel acceptance report. Close the release gates only after those external acceptance records exist.

Until those steps pass, channel-level acceptance is not 100% and the no-hosted-runner policy is not yet accepted organization-wide. The ChaptarrNG worktree has no hosted runner targets or fallbacks, but many of its private routes are intentionally blocked pending runner capacity and exact broker/workflow access.
