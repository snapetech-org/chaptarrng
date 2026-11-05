# PR #190 localization application: W300–W349 Core

This folder contains the review packets, exact-hash application plan, product readback, post-merge reattestation, latest-main comparison, and PR delivery evidence for the W300–W349 Core localization update.

## Applied result

The product commit is `7e69f46e21f1ea7455893179116ec85ee2b0f85e` (tree `3e43bcd87bc103d141109053abb52b70508650a1`), parented directly to the sealed input head `f0082d1b655c05b8d4019ede523dd695cd6d745a` (tree `4cf2322354e9cf9b12d59cf10d0d8661c68ab4af`). Its complete diff is the 11 Core locale catalogs listed in `application-plan/product-commit-record.json` and `merge-reconciliation/product-commit-paths.txt`; no source or workflow path is included.

The plan applies 106 selected target values across 11 catalogs: 87 primary proposals resolved by cross-review (86 accepts and one reviewer-directed revision), plus 19 root suggestions accepted by an independent cross-review. Korean is included among the 12 audited catalogs and had no selected changes. The 48 callerless hash-only holds were excluded; no other unreviewed cells were applied.

## Readback and changed context

The product readback and the portable `application-readbacks/verify_current_head.py` verifier cover all 600 cells (50 source keys × 12 locales), all 12 catalog key counts and key orders, source text hashes, and current caller-context fingerprints. The final exact-head verifier passed at `4688c998d92c821bbc1464ea4640c90615a69327` (tree `e655295664bbe8466e6f5541eb78e17e32cfca12`): 106 selected values match, 494 cells remain unchanged, there are zero source-text or target mismatches, and every catalog has 4,165 keys with preserved order.

The candidate-free context fingerprints differ for `BookMatchingStrictness` and `Selected`; only the selected cell `BookMatchingStrictness::id` has a changed context. The fresh independent review and root adjudication at head `4688c998` accept its current Indonesian value `Mode pencocokan`, bound to caller-context SHA-256 `f920d5f6a8031a4b828e03f93d56f7e0c06653e56202f41fed4b784f670fb74f`. Both confidence ratings are moderate. Native-speaker sign-off and browser review are not claimed. The earlier 0383 and 4f review packets and adjudications are retained as superseded history; the 4688 packet is authoritative.

## Base and merge history

The product commit was followed by non-force base-integration merges `0383b300385b8ec44be0fd38ea5df992230a349d`, `4f099a90891b1a70a3c3ed76e30764ba02f03777`, and `4688c998d92c821bbc1464ea4640c90615a69327`. The only content conflict was `docs/localization/server-message-sources.json`; `merge-reconciliation/` records the three-way resolver, validation, and preservation of valid entries from both sides. Latest-main workflows are intact.

Latest `main` is `76fcc3193f3d08fe4827dca1556b84b2ae74b5df`. Its delta from the PR's pinned base `025498c1a9deb999a4acd1eafc4c807a252e0c29` has four paths, shown in `merge-reconciliation/main-delta-paths-025498-to-76fcc.txt`. It overlaps no PR-diff paths and changes no W300 source text, any of the 600 target values, or any of the 50 caller-context hashes. The local merge-tree is clean; GitHub's PR API reports `mergeable=true` and `mergeable_state=clean`. No extra merge commit was added for this unrelated delta. No workflow path appears in the PR diff.

## Evidence map

- `source-packets/` retains the candidate-free input, primary review, primary cross-review, blind review, root suggestions, root-suggestion cross-review, original root attestation, packet builders, and both review-exposure notes. The discarded first root-suggestion cross attempt is not included.
- `application-plan/` contains the 106-row exact-hash plan and apply scripts/action record.
- `application-readbacks/` contains the product readback and verification, the final 600-cell/50-context readback, and the portable exact-head verifier.
- `review-supplements/` contains the current complete root review seal and the 0383, 4f, and current 4688 fresh-review packets.
- `merge-reconciliation/` contains source-manifest conflict evidence and the latest-main path/context/target comparison.
- `live-snapshots/` contains the PR API/ref/check snapshots and the unmodified original body, exact suffix, expected appended body, and byte-prefix proof.
- `hooks-and-actions.json` records the exact local hook commands skipped and why, plus the automatic Actions run result.

## Checks and hooks

No tests, builds, scans, or CI dispatches were run for this window. The push automatically produced Actions run `41948520500` (`pull_request_target`); all 10 check runs completed `skipped`, with no commit statuses.

The persistent `core.hooksPath` remained `.githooks`. For the documented commits and pushes, `core.hooksPath=/dev/null` was used only as a command-scoped override. The pre-commit hook command `scripts/security-scan-local.sh --staged --semgrep-only` was skipped because the task prohibits local scans. The pre-push hook command `packaging/scripts/test-publish-image-aliases.sh` was skipped because the task prohibits tests. No persistent Git configuration was changed.

The PR remains open and draft. The prior PR body was captured before append at 62,184 bytes with SHA-256 `ff98a2966675d0c331db16845d5608fc0a4b74ddf4faad8f8142b2df7c99d7ce`; `live-snapshots/body-prefix-verification-pre-append.json` confirms the body was byte-identical immediately before this update. The exact appended body is prepared in `live-snapshots/pr-body-expected-after-append.txt`; the post-push API readback proof will be added after the body update.
