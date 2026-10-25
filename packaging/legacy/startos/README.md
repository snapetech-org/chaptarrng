# StartOS BookshelfNG identity handoff

`candidate/` contains the StartOS wrapper source for the existing BookshelfNG
package identity. It keeps package ID `bookshelfng`, volume IDs `main`,
`books`, and `downloads`, the `/config`, `/books`, and `/downloads` mounts,
the `/init` startup contract, port `8787`, and Hardcover compatibility mode
while moving the runtime to ChaptarrNG.

The release candidate builder copies this source into the platform handoff
bundle and replaces its image placeholder with the exact ChaptarrNG
multi-architecture Hardcover digest and sets the StartOS package version to
the matching ChaptarrNG release plus revision `:1`. Do not build or publish
the source template directly. Build and sign the generated candidate inside the external
maintainer's StartOS packaging workspace; this repository does not hold that
workspace key. The external StartOS package repository is not modified by
this candidate; device install, backup, update, and rollback acceptance still
belong to that package's handoff.
