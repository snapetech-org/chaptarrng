---
category: fixed
audience: users, operators
area: packaging
action: none
breaking: false
---
The standard ChaptarrNG image advertises its fresh-install default port `8789`. Legacy BookshelfNG package adapters use that same image digest and preserve their service port through channel configuration; no second compatibility image is built.
