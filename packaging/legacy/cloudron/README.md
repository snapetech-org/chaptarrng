# Cloudron BookshelfNG identity candidate

This candidate keeps the installed Cloudron application ID
`tech.snapetech.bookshelfng`, its `/config`, `/books`, and `/downloads`
persistent directories, port `8787`, and `/ping` health check. The ChaptarrNG
compatibility image enables the first-start migration supervisor and keeps the
native Hardcover mode used by BookshelfNG's Cloudron image. Existing Cloudron
app environment values override that default; preserve `HARDCOVER=false` only
for an installation that previously used the Goodreads image flavor.
If a first-start attempt rolls back cleanly, set
`BOOKSHELFNG_MIGRATION_MODE=retry` only after correcting the cause. An
intervention-required state needs manual recovery.

The package installs a small nginx reverse proxy on Cloudron's stable port
`8787` and binds ChaptarrNG to loopback port `8788`. `/ping` is a liveness
response during migration and recovery, so Cloudron does not restart the
container while the supervisor is converting the database. Other requests are
forwarded to ChaptarrNG with WebSocket, streaming, and forwarded-header support;
they return an actionable migration/recovery page until the app is ready. If
either nginx or the supervised application exits, the package entrypoint stops
the other process and exits so Cloudron can recover the container.

The candidate manifest and Dockerfile are not published. The Dockerfile uses
the source-only `bookshelf-hardcover` image target; before a Cloudron update is
accepted, pin the exact published multi-architecture digest and run install,
update, data backup, rollback, and health checks on Cloudron. Generate
`CloudronVersions.json` with the Cloudron CLI from the accepted package build;
do not create release metadata from an unbuilt candidate.

Cloudron package source historically lives in the BookshelfNG repository. This
ChaptarrNG-owned candidate records the package contract and supplies a safe
source handoff without changing the external Cloudron listing.
