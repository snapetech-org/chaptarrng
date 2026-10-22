---
category: changed
audience: operators
area: distribution
action: none
breaking: false
---
Built `.978` BookshelfNG identity AppImage and Flatpak candidates locally. Both passed an installed-channel update and rollback rehearsal: checksum-verified public `.71` packages were replaced at the same AppImage path or Flatpak app ID, the synthetic schema-158 database migrated to schema 121, and rollback restored the original database byte-for-byte before the public `.71` packages started again and returned `/ping` 200. The pre-fix candidate exposed automatic author-path repairs for records whose configured root equaled the author path. Source now includes a regression fix for this shape, pending hosted validation; representative operator data remains open. These local rehearsals do not establish a live published update channel. The unsigned artifacts are unpublished; no release, catalog listing, or image alias was published.
