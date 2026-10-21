# Co-op Cloud BookshelfNG identity candidate

This source candidate keeps the existing `bookshelfng` recipe and named volume
identities while switching the service to ChaptarrNG's Bookshelf-compatible
Hardcover image target. The `/config`, `/books`, and `/downloads` mounts and
Traefik service port remain the same.

The recipe has not been accepted into the canonical Co-op Cloud collection.
The image target is not published. Before deployment, set
`CHAPTARRNG_COMPAT_IMAGE` to the exact published digest that passed migration
and rollback validation. The recipe still requires external maintainer
acceptance and a real Swarm install, update, backup, and rollback rehearsal.

The external `proxy` network and a reachable Docker Swarm host are required.
