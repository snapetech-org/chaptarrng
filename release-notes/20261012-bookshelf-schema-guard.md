---
category: fixed
audience: operators
area: migration
action: none
breaking: false
---
ChaptarrNG now checks database lineage before running migrations and stops if a non-empty legacy Readarr or Bookshelf database sits beside the native database, preventing legacy data from being silently ignored. Bookshelf databases use the staged SQLite import and backup path; unsupported schemas fail closed.
