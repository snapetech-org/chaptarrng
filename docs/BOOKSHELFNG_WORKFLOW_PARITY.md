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
| Browse and understand the library | Authors, Books, Bookshelf, Calendar, and Unmapped Files are reachable from Library. The Books index has a saved **Both** filter with a format label on each record; editing an author from a book row opens the matching format tab. Bookshelf groups recognized same-work records. Book details include edition and narration information; series details compare narrators and volumes. Failed bookshelf loads have a retry action. | Browse a representative mixed-format library on narrow and wide screens; check empty, loading, retry after a failed request, partial-metadata, and no-file states. |
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

## Feature-level crosswalk

This crosswalk covers the user-visible BookshelfNG feature families called out
in the retirement feature inventory. It maps each one to a ChaptarrNG surface
and keeps source presence separate from end-user acceptance.

| User-facing capability | ChaptarrNG path | End-user acceptance still needed |
| --- | --- | --- |
| Set up audiobook and ebook libraries independently | System > Quickstart, Settings > Media Management, and the per-format author editor configure roots, quality profiles, metadata profiles, and defaults. ChaptarrNG stores the formats as separate records; Bookshelf > Formats brings recognized same-work records together. | Walk through ebook-only, audiobook-only, and mixed setups. Confirm default-root/profile inheritance, explicit per-author overrides, and that one format’s settings never change the other. |
| Choose catalog sources and edition-matching preferences | Settings > Metadata and Settings > Profiles > Metadata configure enabled catalogs, field preferences, language filters, weighted edition terms, and preferred series. | Save and reload choices, then compare real catalog results for language, preferred series, narrator/publisher evidence, and missing metadata. |
| Search by title or identifier | Add New supports provider selection, submit-to-search, optional search-as-you-type, and ISBN/ASIN/provider-ID matching. Existing work and edition IDs retain their provider namespace. | Exercise first and repeated searches, clearing a slow query, same-query resubmission, identifier collisions, provider errors, and preference persistence in a browser. |
| Review editions and narrators before choosing | Book details expose Editions and Narrations; series details compare narrators across volumes. Edition cards retain language, format, narrator, ownership, and monitoring context when the provider supplies them. | Use real provider records with incomplete and conflicting metadata. Confirm unknown details remain unknown and retries recover after an endpoint failure. |
| Recover an existing file that did not match | Library > Unmapped Files and Manual Import provide review and retry paths. Cross-catalog recovery uses exact identifiers or a normalized title-and-author match, and queues the normal pending import instead of attaching a file directly. | Test exact and conflicting ISBN/ASIN/provider IDs, wrong format, duplicate titles, ambiguous results, failed providers, and cancellation with representative files. |
| Monitor titles and search series packs | Author and book editors set per-format monitoring and future-book policies. Wanted and book actions expose searches; series rows provide pack search with per-file Manual Import context. | With a configured indexer, confirm monitored and unmonitored books behave correctly and that pack results can be assigned to the right volumes. |
| Download an ebook from a direct source | Add New and the built-in Direct Download indexer/client handle configured sources, API-key downloads, staged transfer state, and the optional browser fallback. | Use a real source and client to check ordering, credentials, interrupted transfers, redirects, restart recovery, and final import on each supported browser architecture. |
| Reuse a torrent already in the download client | Interactive search can adopt a matching torrent by hash, while automatic searches avoid starting a duplicate. The existing client item remains tracked for seeding. | Check wrong categories, completed and active seeds, client restarts, stale matches, and final import against supported clients. |
| Track imports and retry or cancel preparation | Activity and pending-author import status expose progress and terminal states; retry and cancellation preserve requests shared by multiple users. | Exercise successful fulfillment, partial failure, retry, shared requests, cancellation races, availability/history updates, and restart recovery with the unchanged SeerrNG client. |
| Merge and preserve audiobook chapters | Quality profiles can opt into multipart M4B merging; conversion preserves tagged track order and chapter metadata, reports progress, and has a safe fallback. | Verify representative M4B and MP3 media in Move and Copy modes, including chapter edits, failure recovery, cancellation, and restart/resume. Synthetic fixtures do not close this check. |
| Move files or change format-specific storage safely | Author and bulk editors provide previewable format-aware moves, collision checks, root containment, and registered-sidecar handling. Unknown files stay outside the move. | Review preview completeness, cross-device moves, collisions, partial-failure rollback, external files, and a large mixed-format library. |
| Automate additions from lists and notifications | Settings > Import Lists supports Goodreads and Hardcover sources, preview, exclusions, and durable sync history; notification settings include list and import events. | Verify existing Goodreads credentials and Hardcover list responses, private/owned-list behavior, notification delivery, rollback, and secret redaction. |
| Connect library, reader, and delivery services | Settings > Connect and Notifications configure AudioBookShelf, Calibre, Grimmory, BookLore BookDrop, BookOrbit Book Dock, and Emby/Jellyfin workflows. Each integration keeps its own direction and file-ownership rules. | Connect real services and confirm uploads, refreshes, metadata direction, partial-batch recovery, supported file types, and no duplicate processing. |
| Request and monitor books through SeerrNG | The restricted SeerrNG API exposes capabilities, format-scoped libraries, edition/narrator data, and pending-import actions without granting global administration. | Synthetic migrated-record reads and request create/poll/cancel pass. Still verify successful acquisition, retries, availability/history, reverse proxies, and unchanged production settings with live services. |
| Restore a portable or encrypted backup | System > Backup handles scheduled archives, encrypted portable archives, and recognized BookshelfNG restores; unsupported or unsafe inputs fail closed. | Restore a representative backup and profiles, check wrong passphrases and tampering, exercise rollback, and confirm secrets stay out of logs. |
| Diagnose and operate the installation | System > Status, Tasks, Events, and Log Files expose operational state; MQTT, Prometheus, and aggregate diagnostics are optional integrations. | Check the installed service’s permissions, restart and failure recovery, metrics/events, diagnostics opt-in, and log redaction. |

The detailed database migration, installer and release-channel checks remain in
the [retirement status](BOOKSHELFNG_RETIREMENT.md). They are not considered
accepted by this user-workflow map.

## Known review gaps

The source map includes substantial user-facing coverage, but full live parity
acceptance is still open. A disposable source build from ChaptarrNG `main` at
`c0aabe44` was exercised on 2026-10-20 with the checked-in BookshelfNG schema-158
fixture. SeerrNG's `ReadarrAPI` read the migrated audiobook and ebook records;
the restricted key could read capabilities and both libraries but could not
access host configuration or unrelated commands; the global key retained that
access. A second run created, polled, and cancelled a synthetic pending author
request through the adapter. A separate HTTP regression probe for the bare
numeric Hardcover ID fix resolved `2459845` through both audiobook and ebook
facades and returned the canonical `hc:2459845` identity.

On the `c0aabe44` source snapshot, the full Chaptarr Core suite passed with
4,496 tests passed, 15 skipped, and no failures; focused book-lookup and
edition-selection tests passed 11/11. The current frontend Node suite passed
29/29, including the paged-book numeric-ID regression, and focused ESLint passed
for the table, poster, and overview book-index components changed in this pass.
Browser QA is still outstanding; isolated browser workspace tools, configured
live metadata providers, and download-client test services were unavailable in
this session. These results verify synthetic HTTP integration and automated
source behavior, not every workflow in the table. Browser checks and live
provider/client acceptance remain open.

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
