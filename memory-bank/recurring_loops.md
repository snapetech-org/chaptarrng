# Recurring loops

## Shared run labels are not job identity

**Symptom:** a JIT guest requested for one job receives another queued job from the same run.

**Why:** GitHub schedules by the full `runs-on` label set; a candidate job ID in the runner name does not constrain GitHub's match.

**Resolution:** derive a unique `gha-<run-id>-<workflow-job-id>` label from the exact workflow source, add zero-based `strategy.job-index` for matrices, and reject duplicates before and after runner creation. Verify the actual GitHub job ID, name, state, and runner assignment.
