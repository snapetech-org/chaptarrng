# Self-hosting catalog and Compose handoff

This Compose service is the ChaptarrNG candidate for legacy BookshelfNG catalog
entries that allow an existing user to edit the image field. It keeps the
`bookshelfng` container name, default port `8787`, and the original named volumes for
`/config`, `/books`, and `/downloads`. The default image is the source-only
BookshelfNG-compatible ChaptarrNG target; set `CHAPTARRNG_COMPAT_IMAGE` to an
immutable published digest before accepting an update.
The candidate enables the first-start migration supervisor so an unsuccessful
conversion restores the retained BookshelfNG database and configuration.
Set `BOOKSHELFNG_MIGRATION_MODE=retry` only after a clean rollback and after
correcting the cause of the first failure; an intervention-required state needs
manual recovery.
The old catalog Compose recipe used the native Hardcover image, and the
candidate preserves that default. Set `HARDCOVER=false` when migrating a
prior softcover image so bare provider IDs keep their Goodreads meaning.
Set `BOOKSHELFNG_PORT` to the existing container port and
`BOOKSHELFNG_HOST_PORT` to the existing host port when they differ from
`8787`. The fresh-install app default follows the container port. A saved
`config.xml` port remains authoritative, and the health check keeps the
container unhealthy if that port no longer matches the Compose mapping.

CasaOS, ZimaOS, Umbrel, TrueNAS, Cosmos, CapRover, Portainer, and other catalog
records are maintained in their respective external repositories. This file
does not claim that those listings have been changed or that their UI update
and rollback flows have been tested. Keep each catalog entry on BookshelfNG
until its owner updates and accepts the same identity, mount, update, backup,
and rollback contract.
