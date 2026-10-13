---
category: security
audience: operators
area: ci-security
action: none
breaking: false
---

Validation snapshots omit extensionless authentication, token, credential, and secret files while retaining source code with those names. The trusted validation launcher pins sandbox mount sources through inherited file descriptors before masking host paths. Per-task diagnostic logs are capped at 16 MiB and marked when output is truncated.
