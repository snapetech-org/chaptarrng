# BookshelfNG user workflow parity map

This map covers the user-facing search and acquisition workflows that matter
when someone uses ChaptarrNG in place of BookshelfNG. It describes the current
source tree, not the behavior of a published image.

| User task | ChaptarrNG source status | What still needs acceptance |
| --- | --- | --- |
| Search the catalog from Add | Search waits for **Search** or Enter by default. **Search as you type** is an optional browser preference. Clearing or changing a query cancels pending and active requests. Existing catalog choices and result filters remain available. | Browser-check first search, Enter, repeated same-query search, preference persistence, and clearing during a slow request. |
| Search for releases for one book | Automatic search and interactive release search are available from book rows and details. The two icon actions now expose their names to keyboard and screen-reader users. | Confirm indexer results, grab, queue, and manual import in a running instance. |
| Search for a series pack | Each series now has a **Search for a series pack** action. The modal submits the series and author IDs; the release API searches that series and retains its books for the existing manual import flow. | Confirm an indexed pack reaches the queue and that Manual Import can assign each file to the right book and media format. |
| Adopt a torrent that is already in the download client | A duplicate-torrent response opens an **Adopt Existing Torrent** confirmation. ChaptarrNG rechecks the torrent hash, tracks the existing client item, and avoids submitting it to the indexer or starting another download. Automated searches skip a matching existing torrent. | Confirm matching client/category, wrong category, completed and seeding states, stale results where the torrent disappears before confirmation, and recovery when the client cannot be queried. |
| Download an ebook from a direct source | The Direct Download indexer and built-in download client are present in source, with API-key and optional browser-fallback setup. A separate download client is not required for this path. | Verify a supported source and the target container architecture, including retry and import after restart. |
| Stop tracking a download but leave it in the client | The queue removal modal offers **Ignore** for tracked downloads and explains the available removal choices. | Confirm the client item remains untouched and the ignored history behaves as expected. |

This map follows the Add, search, grab, import, and queue workflows described
in BookshelfNG's search-and-download guide. The search defaults, series-pack
search, and existing-torrent adoption changes are in ChaptarrNG source. They
have not been exercised against a running backend, indexer, or download client
in this pass.

This map covers the request and acquisition path raised in this conversation,
not every BookshelfNG capability. The broader feature, migration, integration,
client, and release-channel comparison remains open in
[`FORK_FEATURES.md`](FORK_FEATURES.md); those workflows need their own behavior
and upgrade acceptance before a deployment can be called fully equivalent.
