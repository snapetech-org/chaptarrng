# Cloudron BookshelfNG identity candidate

This candidate keeps the installed Cloudron application ID
`tech.snapetech.bookshelfng`, its `/config`, `/books`, and `/downloads`
persistent directories, port `8787`, and `/ping` health check. The ChaptarrNG
compatibility image runs the existing BookshelfNG database migration from
`/config` during startup.

The candidate manifest and Dockerfile are not published. The Dockerfile uses
the source-only `bookshelf-hardcover` image target; before a Cloudron update is
accepted, pin the exact published multi-architecture digest and run install,
update, data backup, rollback, and health checks on Cloudron. Generate
`CloudronVersions.json` with the Cloudron CLI from the accepted package build;
do not create release metadata from an unbuilt candidate.

Cloudron package source historically lives in the BookshelfNG repository. This
ChaptarrNG-owned candidate records the package contract and supplies a safe
source handoff without changing the external Cloudron listing.
