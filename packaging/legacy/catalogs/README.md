# Self-hosting catalog and Compose handoff

This Compose service is the ChaptarrNG candidate for legacy BookshelfNG catalog
entries that allow an existing user to edit the image field. It keeps the
`bookshelfng` container name, port `8787`, and the original named volumes for
`/config`, `/books`, and `/downloads`. The default image is the source-only
BookshelfNG-compatible ChaptarrNG target; set `CHAPTARRNG_COMPAT_IMAGE` to an
immutable published digest before accepting an update.

CasaOS, ZimaOS, Umbrel, TrueNAS, Cosmos, CapRover, Portainer, and other catalog
records are maintained in their respective external repositories. This file
does not claim that those listings have been changed or that their UI update
and rollback flows have been tested. Keep each catalog entry on BookshelfNG
until its owner updates and accepts the same identity, mount, update, backup,
and rollback contract.
