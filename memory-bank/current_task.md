# Current task: Bind each JIT runner to its exact workflow job (2026-10-24)

- **Goal:** stop GitHub from assigning an unrelated queued ChaptarrNG job to a JIT guest selected for another job.
- **Story ID:** JIT-QUEUE-001 in [work_breakdown.md](work_breakdown.md).
- **Scope:** add a unique run and YAML-job label to each trusted Linux JIT selector; use the matrix index where the job has a matrix. Preserve hosted fallback, tag-only release builds, owner-PR trust checks, and runner capacity gates.
- **Status:** workflow changes are in the existing draft PR #159. Packer's broker fix is pushed to PR #3. Three JIT guest units remain active on the primary host; no pause or admission-marker change was made.
- **Verification:** 187 focused Packer policy tests pass. All 165 workflow files parse and the label audit covers 216 exact job contracts, including 25 matrix jobs.
- **Next step:** push this workflow fix, inspect PR checks and actual job assignments, then deploy the broker after active guests finish and verify a live queue canary.
- **Limits:** do not pause admission, restart the broker while a guest is active, or weaken resource and pressure gates.
