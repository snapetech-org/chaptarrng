# Work breakdown

## 2026-10-24 — JIT job assignment isolation

Objective: bind every broker-created disposable guest to the exact trusted workflow job selected, while active builds keep running.

| Story ID | Scope | Status |
| --- | --- | --- |
| JIT-QUEUE-001 | Add a unique run/job/matrix label to trusted Linux JIT jobs across the nine applications; verify exact workflow source and reject duplicate labels in the Packer broker. | Packer tests pass and Packer PR #3 is updated. The ChaptarrNG workflow fix is staged for PR #159; deployment and a live canary remain open. |

Guardrail: do not pause or restart the live broker while a guest is active; keep CPU, memory, PSI, disk, and hard-slot gates unchanged.
