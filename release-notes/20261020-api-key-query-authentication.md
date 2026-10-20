---
category: security
audience: users, operators
area: authentication
action: none
breaking: false
---
Operators can disable API keys in URL query strings with `CHAPTARR__AUTH__ALLOWAPIKEYQUERY=false`; the legacy `BOOKSHELF_ALLOW_API_KEY_QUERY=false` setting still works when the ChaptarrNG option is unset. Header credentials and SignalR access tokens are unchanged.
