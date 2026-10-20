# BookshelfNG user workflow parity map

This map compares the everyday work a BookshelfNG user needs to do in ChaptarrNG.
It describes source affordances, not a published image. BookshelfNG retirement
is not yet accepted, so ChaptarrNG releases and migration channels remain
frozen.

“Available in source” means a user-facing route or action is present. It does not
mean the workflow has been exercised against a real provider, download client,
representative library, or installed build.

| User task | ChaptarrNG source and affordance | Remaining end-user acceptance |
| --- | --- | --- |
| Get a new instance ready | System > Quickstart checks format roots, profiles, indexers, clients, and optional integrations. Settings now groups destinations by Library, Search, Connect, General, and System, with Quickstart linked from both Settings and System. | Walk through a clean setup on desktop and mobile, including missing roots, an empty indexer list, and a failed provider test. |
| Choose the default format for new items | Add New dialogs share a saved default format. Users can change it in Settings > UI or with **Set as default** in an add dialog; individual items can still override it. | Verify persistence after reload and confirm author, book, and series dialogs all start with the saved value. |
| Add an author, book, or series | All three flows support audiobook, ebook, or both where relevant; root folders and quality profiles are visible first, with monitoring, metadata, and tags under **More options**. Missing setup links to Quickstart. Root-folder loading failures show a retry. | Exercise each format, both formats, saved defaults, one-item overrides, missing roots/profiles, existing-author book adds, and add failures. |
| Find a title in the catalog | Add New has provider selection, result filtering, and submit-to-search by default; Search as you type is optional. Query changes and clearing cancel stale requests. | Check first search, Enter, repeated same-query search, preference persistence, provider errors, and clearing during a slow request. |
| Browse and understand the library | Authors, Books, Bookshelf, Calendar, and Unmapped Files are reachable from Library. The Books index has a saved **Both** filter with a format label on each record; Bookshelf groups recognized same-work records. Book details include edition and narration information; series details compare narrators and volumes. Failed bookshelf loads have a retry action. | Browse a representative mixed-format library on narrow and wide screens; check empty, loading, retry after a failed request, partial-metadata, and no-file states. |
| See ebooks and audiobooks together in Books | The Books index **Both** filter lists every audiobook and ebook as its own record and shows its format label. Monitoring, status, narrator controls, and quality profiles use that record's format. | Verify paging, custom filters, sort order, per-format profiles, monitoring labels, selected-row edits, and reload persistence across both formats. |
| See related ebooks and audiobooks together in Bookshelf | Bookshelf's **Formats** view loads both formats in one pass, groups records only when canonical Hardcover, Goodreads, or Open Library work IDs match, and leaves unrelated or unidentified records separate. Each format keeps its own progress, detail link, and monitor toggle; bulk monitoring applies to both. | Check matched, unmatched, duplicate-edition, missing-ID, and provider-alias records; switch formats repeatedly, reload the page, retry failures, and confirm bulk and per-book monitoring. |
| Move between the ebook and audiobook for the same work | Search results link to tracked records in each format. Book details label both formats, link to same-work records, and offer the missing format in the standard add modal when the work and author have provider IDs. The add action appears only after the server confirms no same-work record exists. | Confirm identity matching never links unrelated works; test duplicate editions, missing identity, endpoint failure/retry, and successful add against real catalog data. |
| Decide what to monitor | Add flows and author/book editors expose per-format monitoring, initial-book monitoring, and future-book policies. In **Formats**, each book keeps its own monitor toggle and the bulk action applies to both formats. Wanted has Missing and Cutoff Unmet pages. | Confirm per-book changes affect only one format, bulk changes affect both, and automatic searches honor the chosen monitoring state. |
| Search and grab a release | Book actions offer automatic and interactive release search. Series rows can search for a pack and send files to Manual Import. Direct Download provides an ebook-only source path. | With a supported indexer and client, search, inspect, grab, retry, and import representative audiobook and ebook releases. |
| Reuse a torrent already in the client | Duplicate-hash detection can offer **Adopt Existing Torrent**; automatic searches skip matching torrents. Adoption tracks the existing item instead of starting a second download. | Check wrong categories, completed and seeding states, stale results, client outages, restart recovery, and final import. |
| Track or stop a download | Activity groups Queue, History, Blocklist, and Ignored Downloads. Queue removal explains ignore and remove choices. | Confirm ignore leaves the client item untouched; verify retry, blocklist, client removal, and history after restart. |
| Import or recover files | Manual Import and Unmapped Files provide review paths. Pending-author imports retain requested books and formats while author preparation retries; retry and cancellation are exposed. | Validate ambiguous matches, multi-file assignment, provider failure, shared requests, retry, and cancellation against representative files. |
| Organize files safely | Media-management settings configure roots and naming. Format-aware move/retag workflows preview affected files, check destination conflicts, and preserve linked sidecars. | Exercise preview, copy/move, mixed roots, collisions, interrupted moves, external files, and a large library. |
| Automate from lists | Import Lists supports Preview Next Sync and per-list sync history, including read-only preview counts and provider-state commit status. | Test supported list providers with an unchanged saved configuration, then compare preview with a later scheduled sync. |
| Connect external tools and services | Settings > Connect, Metadata, Indexers, Download Clients, and Import Lists expose providers. The SeerrNG contract has capability discovery and format-aware operations; request dispatch qualifies numeric provider work IDs, and Editions/Narrations failures offer a visible retry. | Focused regressions now cover bare numeric Hardcover lookup IDs through the format facade and plus-encoded series titles through the restricted-key policy. Still verify saved credentials, notifications, request fulfillment with a live provider, reverse proxies, and unchanged clients. |
| Back up and restore | System > Backup supports the app’s scheduled and portable backup paths; current source includes BookshelfNG archive restore and encrypted export/restore. | Restore a representative backup, verify profiles and settings, test wrong-passphrase and rollback paths, and confirm no secret values appear in logs. |
| Diagnose and maintain the service | System exposes Status, Tasks, Updates, Events, and Log Files; optional metrics and MQTT have separate settings and docs. | Check service restart, task failure, log access, metrics, MQTT discovery, and recovery on the target installation. |

## Known review gaps

The source map includes substantial user-facing coverage, but I have not completed
a live end-to-end parity run. The local pass did not have an isolated browser QA
workspace available, nor configured provider and download-client test services.
So the UI changes in this pass have source-level and automated validation only;
the browser checks in the table remain open.

The combined shelf is a presentation over ChaptarrNG's separate audiobook and
ebook records. It does not turn them into one database record or combine their
monitoring and file state. Cross-format work links rely on canonical work IDs;
records without shared work identity remain ungrouped.

The BookshelfNG-to-ChaptarrNG path inventory also has unresolved semantic review.
That inventory is useful for finding code that needs inspection, but a path count
is not a user-facing feature gap by itself. Keep migration, installed update
channels, and release readiness in
[the retirement status](BOOKSHELFNG_RETIREMENT.md), separate from this workflow
map.
