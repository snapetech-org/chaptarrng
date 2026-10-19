---
category: added
audience: users, operators
area: installation
action: none
breaking: false
---
A draft Windows installer now migrates the BookshelfNG Readarr service to ChaptarrNG while retaining its data directory and a verified rollback backup. It previews before apply, verifies the SQLite database and sidecars stayed unchanged, and attempts to restore BookshelfNG if handoff fails. The candidate is unpublished and currently supports a single SQLite database using the standard LocalService account.
