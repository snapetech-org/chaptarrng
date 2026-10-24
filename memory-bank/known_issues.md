# Known issues

## Shared runner labels can cross-assign a queued job

GitHub may assign any same-run job matching a shared run label, even when the broker selected a different job. The fix adds a workflow-source-verified label for the YAML job ID and matrix index. ChaptarrNG's change is staged in PR #159; until the broker and workflows are deployed together, do not claim exact job assignment is live.
