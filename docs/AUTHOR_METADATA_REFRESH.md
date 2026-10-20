# Author metadata refresh

ChaptarrNG stores the current author catalog in its database. `LastInfoSync`
records when an author was last checked; older records are not deleted just
because they are stale. The provider response caches are in memory and do not
replace this saved catalog.

## Automatic refresh policy

ChaptarrNG evaluates these rules in order:

| Author state | Decision |
| --- | --- |
| `LastInfoSync` is missing | Refresh metadata. |
| Last sync was more than 30 days ago | Refresh metadata. |
| Last sync was within the past 12 hours | Skip metadata refresh. |
| Status is `Continuing` and last sync was more than 2 days ago | Refresh metadata. |
| Latest known book release is within the past 30 days | Refresh metadata. |
| No rule above matches | Wait for a later sweep or explicit refresh. |

The 30-day stale check runs before the 12-hour guard. The 12-hour guard runs
before status and release-date checks, so an author checked recently is not
fetched repeatedly because of a new book event.

## When a book is added

On current ChaptarrNG `main`, a book-added event checks the author freshness
policy before requesting remote author metadata. It still requests an
author-scoped folder rescan so files already on disk can be found, even when
the metadata check is skipped or returns unchanged data. The rescan follows
the configured **Rescan After Refresh** policy and requires usable folder
evidence for that author. Preview this source-only behavior separately from
the published `v0.9.977` release; the BookshelfNG retirement release freeze is
still active.

Adding a new author performs an initial metadata refresh and folder scan.
Changing an author's metadata profile or adding a format-specific root also
queues a forced metadata refresh so ChaptarrNG can apply the new profile or
hydrate the newly available format path.

## Hardcover request pacing

The native Hardcover provider uses a process-local response cache:

| Response | Cache lifetime |
| --- | ---: |
| Author details | 30 minutes |
| Work details | 6 hours |
| Search results | 10 minutes |

The client spaces requests at one per second in each ChaptarrNG process. It
honors rate-limit reset information and `Retry-After`; when a 429 response has
no usable reset, it pauses requests for one minute. Background catalog work
leaves the final 10% of the reported daily quota for interactive searches.
Cache and pacing state are not shared between separate ChaptarrNG processes.

## Code references

- [Automatic freshness policy](../src/NzbDrone.Core/Books/Utilities/ShouldRefreshAuthor.cs)
- [Book-added event](../src/NzbDrone.Core/Books/Handlers/BookAddedHandler.cs)
- [New-author refresh](../src/NzbDrone.Core/Books/Handlers/AuthorAddedHandler.cs)
- [Profile and format-root refresh](../src/NzbDrone.Core/Books/Handlers/AuthorEditedHandler.cs)
- [Refresh and rescan handling](../src/NzbDrone.Core/Books/Services/RefreshAuthorService.cs)
- [Hardcover provider cache and request pacing](../src/NzbDrone.Core/MetadataSource/Hardcover/HardcoverMetadataProxy.cs)
