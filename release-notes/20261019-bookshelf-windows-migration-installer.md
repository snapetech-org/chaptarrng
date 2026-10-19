---
category: added
audience: users, operators
area: installation
action: none
breaking: false
---
A draft Windows installer can move a BookshelfNG Readarr service to ChaptarrNG, preserving its data path and verified rollback backup. It previews before apply, checks SQLite and sidecar immutability, and restores BookshelfNG if handoff fails. The unpublished candidate supports one SQLite database with the standard LocalService account.
