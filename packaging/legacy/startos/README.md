# StartOS BookshelfNG identity handoff

The existing StartOS wrapper lives in the separate
[`bookshelfng-startos`](https://github.com/snapetech-org/bookshelfng-startos)
repository. ChaptarrNG's source image carries the compatible `/init` launcher,
exposes port `8787` for the BookshelfNG target, and preserves the `/config`,
`/books`, and `/downloads` mount contract. The StartOS package must keep its
`bookshelfng` package ID and its `main`, `books`, and `downloads` volume IDs
when its owner updates the image.

The candidate image target is source-only. The external package still points
at BookshelfNG; no StartOS package update, device install, backup/restore, or
rollback has been accepted. Once a compatibility image is released, update the
external package to its exact digest and validate it on a StartOS device before
recommending a switch.
