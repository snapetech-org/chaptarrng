# StartOS BookshelfNG identity handoff

`candidate/` contains the StartOS wrapper source for the existing BookshelfNG
package identity. It keeps package ID `bookshelfng`, volume IDs `main`,
`books`, and `downloads`, the `/config`, `/books`, and `/downloads` mounts,
and the `/init` startup contract while moving the runtime to ChaptarrNG.

The release candidate builder copies this source into the platform handoff
bundle and replaces its image placeholder with the exact ChaptarrNG
multi-architecture Hardcover digest. Do not build or publish the source
template directly. The external StartOS package repository is not modified by
this candidate; device install, backup, update, and rollback acceptance still
belong to that package's handoff.
