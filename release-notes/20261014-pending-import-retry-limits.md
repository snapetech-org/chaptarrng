---
category: fixed
audience: users, operators
area: import
action: none
breaking: false
---
A queued author import that is still retrying no longer re-adds an author you removed and excluded, and imports that keep failing now stop with a failed status after about a week of attempts instead of retrying forever. Retry Now starts a fresh window.
