---
category: security
audience: operators
area: ci-security
action: none
breaking: false
---

Local validation snapshots exclude dotted and extensionless credential names, private/API/access-token material, and non-source files beneath credential folders while retaining source modules such as TokenStore.ts. The sandbox pins inputs before host paths are masked, and task logs are capped with a visible truncation marker.
