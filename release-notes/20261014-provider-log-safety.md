---
category: fixed
audience: users, operators
area: security
action: none
breaking: false
---
Metadata-provider identifiers now pass through newline cleaning before they reach logs, and rejected OIDC sign-in claims are no longer logged. The existing policy-denial redirect remains unchanged.
