---
category: security
audience: operators
area: ci-security
action: none
breaking: false
---

Local validation snapshots exclude credential files even when extensionless or stored in credential folders, while retaining token-named source code. The sandbox pins inputs before host paths are masked, and task logs are capped with a visible truncation marker.
