# Fixed server and notification copy review

The core catalog review landed independently. This follow-up adds stable, source-fingerprinted coverage for fixed backend and provider copy; its new target-language generation and headed Agentic VM acceptance are still queued. Agent review does not claim qualified native-speaker approval.

## Source coverage

- Server/API, validation, operational progress, and persisted failure messages: 486 distinct messages and 649 unique source callers (657 template/branch bindings). One wholly dynamic Telegram exception/status template is excluded because it contains no application wording.
- Provider field labels/help/warnings: 409 mapped sources and 643 callers, with all 811 explicit schema attribute values resolving to catalog IDs.
- Static provider dropdown labels and explanatory hints: 136 sources, 174 callers, 27 enum types, and 165 enum members. Numeric values, member identifiers, protocol `EnumMember` values, dynamic provider option names, and example hints stay unchanged.
- Outbound human-readable headings, bodies, fields, tests and sensor names: 57 sources and 108 callers, plus two explicitly fingerprinted exclusions for the Proper release marker and retag metadata field names. Transport keys, event names, topics, paths, book/author titles and provider responses stay data.

All seventeen authored loading messages also retain their original no-repeat variety behind stable IDs, with contributor/product names protected. The runtime fallback data is checked against its source inventory.

The union adds 1,065 English IDs to the 2,950-ID baseline: 4,015 sources and 12,780 new locale cells across twelve target languages. Registries carry stable IDs, source fingerprints, exact caller lines/hashes, and protected literal rules. `check-localized-source-coverage.cjs` and `check-server-message-catalogs.cjs` reject stale sources, missing target values, unsafe paths and placeholder/protected-value changes. `--allow-pending` is a preparation check, not final translation acceptance.

## Presentation callers

`server-message-callers.json` fingerprints every introduced presentation boundary. Reviewed paths include:

- Form result alerts, input help/errors/warnings, OAuth and enhanced-select validation. Multiple validation values are translated individually before joining.
- Provider notices for indexers, download clients, notifications and import lists; root-folder/list save failures; import-list synchronization preview/history; first-run authentication and Quickstart connection/Hardcover failures.
- Backup/restore/settings alerts, author media moves, queue status/detail/popovers, pending imports, import-progress drawers/sidebar messages, health status and persisted history details.
- Search-series fetch/save failure presentation and shared `getErrorMessage` consumers. Other search/action connectors retain raw server text until the sidebar or shared error renderer presents it.
- Proxy connection details are matched before insertion into a localized outer sentence. Unmapped-files log-preview failures and naming-validation fallback errors use the same bounded matcher.

The matcher handles only registered fixed wording, prefers exact matches, anchors templates, preserves captured strings byte-for-byte, and refuses ambiguous delimiters, conflicting matches, wholly dynamic patterns and oversized/non-string inputs. Literal examples such as `hc:{id}` remain literal. Known M4B singular/plural CPU wording maps to one count-preserving source template. Unknown messages and diagnostic exception/stack/provider payloads remain unchanged.

## Backend/schema and outbound callers

SchemaBuilder caches untranslated metadata and localizes cloned fields/options for each response, including tokens from the declaring settings type. This prevents labels from staying in a previous UI language. Sensitive values, saved configuration values and underlying enum values keep their existing paths. Regression tests switch languages after cache creation and confirm the original cached metadata and example hints stay intact.

NotificationFactory configures each provider instance with the current localization service. Shared headings and directly constructed notification bodies use stable keys; proxy-level test senders receive the service through dependency injection. One-pass substitution prevents a dynamic title/path containing braces from becoming another template. Home Assistant display names follow UI language changes through a locale-sensitive worker fingerprint; MQTT keys, IDs, enums, units and Jinja templates remain unchanged.

Download failures carry an explicit translation ID only when the app authored that failure. A provider error that happens to equal an app phrase has no ID and stays raw. History retains its original message while outgoing human copy uses the explicit ID/tokens. Health-check text already uses existing catalog-backed localization; forwarded provider status/exception fields remain data. Deletion bodies preserve author/book names and retain DTO English fallback compatibility for direct callers.

No notification is sent by localization tests. All notification transports are mocked. PR #181 and #182 discussion/review timelines were checked; both contain no review feedback to resolve. Final delivery requires strict coverage, all twelve translated catalogs, the full new-source semantic review, current-head tests/build, and headed provider/schema/RTL review in the Agentic VM.

## Independent review checkpoint

The coordinating agent independently reviewed matcher/presentation routing, raw schema metadata caching and per-response localization, one-pass notification substitution, provider setup, download-failure provenance/persistence and locale-sensitive MQTT fingerprints. No blocking issue was found in that pass; the outbound-provider and full caller review continues. The commit hook also flagged a dynamically constructed parameter-token regular expression. The parser now uses a static `valueN` token expression and a validated-name set, eliminating that construction while retaining bounded fixed-part matching.

## Headed mixed-direction review

A task-owned Chaptarr instance at `http://192.168.50.148:18879` was reviewed in an isolated headed Agentic VM Chrome session. Arabic root-folder validation reproduced reordered Latin path punctuation and mixed-script filenames. The presentation helper now inserts FSI/PDI boundaries around each captured value; a repeated real failing save and a URL/query/port/PUID-shaped capture confirmed the rendered correction. Raw HTTP400 messages remain exact and contain no display controls. Three screenshots and raw-response evidence are retained under `chaptarrng-artifacts/vm-review-capture-v1`; both the browser session and preview backend were closed. Four temporary Arabic values in copied build output enabled this implementation test; they are not repository translations or native approval. Final translated provider/schema/RTL/narrow-layout acceptance is still pending.

The same flow exposed two existing Arabic labels using broadcast/press-media wording for a book/audio library. `MediaManagement` and `MediaLibrary` now use the existing settings-header terminology for library media, with current-source draft provenance. No source IDs or frozen new-source inputs changed for those two contextual corrections.
