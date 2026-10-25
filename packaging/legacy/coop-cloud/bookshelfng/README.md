# BookshelfNG to ChaptarrNG Co-op Cloud handoff

<!-- metadata -->

* **Category**: Apps
* **Status**: 0, unpublished migration candidate
* **Image**: [ChaptarrNG](https://github.com/snapetech-org/chaptarrng)
* **Backups**: 1
* **Healthcheck**: 1
* **SSO**: N/A

<!-- endmetadata -->

This candidate keeps the existing `bookshelfng` recipe name, Swarm stack,
service name, and named volumes while changing the image to ChaptarrNG's
BookshelfNG compatibility target. The existing `/config`, `/books`, and
`/downloads` data remains mounted at the same paths. The recipe backs up all
three named volumes through the Co-op Cloud backup labels.

First startup runs the normal ChaptarrNG migration supervisor. The candidate
uses stop-first deployment and a long startup health window so two application
processes never open the same database and a long conversion is not mistaken
for a failed deployment. Swarm pauses a failed update instead of automatically
restarting an older application against a converted database. The supervisor
restores the BookshelfNG source automatically when first-start conversion or
health validation fails. Keep an independent Co-op Cloud backup before
upgrading; an operator-directed rollback after a successful migration must
restore the matching BookshelfNG data backup before starting the old image.

The recipe defaults to the native Hardcover mode used by the previous Co-op
Cloud image. Set `HARDCOVER=false` only when the old service used the softcover
image. The default internal port is `8787`; keep `BOOKSHELFNG_PORT` equal to the
old Traefik service port. If the saved `config.xml` port differs, the health
check reports the mismatch instead of treating the instance as ready. After a
clean automatic rollback, set `BOOKSHELFNG_MIGRATION_MODE=retry` only after
correcting the cause. An intervention-required state needs manual recovery.

The recipe has not been accepted into the canonical Co-op Cloud collection.
The image target is not published. Before deployment, use the generated recipe
bundle pinned to the exact published digest that passed migration and rollback
validation. The recipe still requires external maintainer acceptance and a
real Swarm upgrade, backup, failure-recovery, and rollback rehearsal.

The external `proxy` network and a reachable Docker Swarm host are required.
