# Co-op Cloud BookshelfNG identity candidate

This source candidate keeps the existing `bookshelfng` recipe and named volume
identities while switching the service to ChaptarrNG's Bookshelf-compatible
image target. It enables the first-start migration supervisor and defaults to
the native Hardcover mode used by the previous Co-op Cloud recipe. Set
`HARDCOVER=false` only when migrating a prior softcover image. The
`/config`, `/books`, and `/downloads` mounts and Traefik service port remain
the same. The default port is `8787`; set `BOOKSHELFNG_PORT` in the existing
stack environment when the prior service used a different port. If a saved
`config.xml` port differs from the routed port, the health check reports the
mismatch instead of treating the instance as ready.
After a clean rollback, set `BOOKSHELFNG_MIGRATION_MODE=retry` only after
correcting the cause. An intervention-required state needs manual recovery.

The recipe has not been accepted into the canonical Co-op Cloud collection.
The image target is not published. Before deployment, set
`CHAPTARRNG_COMPAT_IMAGE` to the exact published digest that passed migration
and rollback validation. The recipe still requires external maintainer
acceptance and a real Swarm install, update, backup, and rollback rehearsal.

The external `proxy` network and a reachable Docker Swarm host are required.
