---
category: security
audience: operators
area: ci-security
action: none
breaking: false
---

Local validation excludes dotted and extensionless credentials, private/API keys, access/refresh-token and client-secret material, and non-source files under credential folders. Source modules such as TokenStore.ts remain available. The sandbox pins inputs before host paths are masked, and task logs are capped with a visible truncation marker.
