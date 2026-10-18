---
category: added
audience: users, operators
area: import-matching
action: Review pending-author suggestions created when unmapped files match one exact catalog work.
breaking: false
---

Unmapped-file retries can now search enabled catalogs after local and V5 matching fail. ChaptarrNG queues a pending-author import only when identifiers or normalized title and author uniquely match one work; it leaves ambiguous files unmatched and never attaches them directly.
