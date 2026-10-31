# Localization workflow

## Stable message IDs

Existing catalog keys are semantic PascalCase identifiers, so they remain valid compatibility IDs. Keep them unchanged when copy is edited. Add new messages with a lowercase namespace and stable identifier, for example ui.language.inheritedFallbackNotice; do not use English copy as a key.

The source catalog is src/NzbDrone.Core/Localization/Core/en.json. docs/localization/source-message-hashes.json snapshots a SHA-256 fingerprint for every key and English source value. A copy edit changes that key's fingerprint and makes the catalog check fail until the snapshot is reviewed and regenerated.

## Locale registry and fallback

src/NzbDrone.Core/Localization/Core/ui-locales.json is the shared registry used by the server and frontend. It defines the 37 supported BCP 47 choices, stable integer setting IDs, exact catalog file, direction, and explicit regional parent fallback. Current saved language IDs outside that set remain available in Settings only when they are already selected, so old preferences can be changed without losing them.

The server merges English, the declared parent catalog, then the exact locale catalog. Missing entries therefore inherit safely without copying English text into every file. The Settings page tells users that untranslated labels appear in English.

## Current target-locale review status

The October 30, 2026 audit of the agreed 12 locales found 220 source IDs absent from every exact target catalog; each resolves to English fallback. Additional exact catalog values that still equal the English source are also reported as inherited. This follow-up records 110 reviewed corrections and consistency fixes, but the remaining legacy exact translations have no review provenance. Full target-catalog translation and native-speaker quality review remain outstanding.

## Per-string provenance

docs/localization/provenance.json stores explicit source-fingerprinted `reviewed` and `drafts` records per locale and message ID. The generated catalog report distinguishes:

- **reviewed** values whose current English fingerprint was reviewed and whose exact locale catalog contains the translation;
- **machine drafts** explicitly recorded under `drafts`, with the current source fingerprint;
- **unreviewed exact** legacy catalog values with no provenance record;
- **inherited** values supplied by the configured parent locale or English fallback.

Existing translations without contributor or workflow provenance are recorded as unreviewed, not assumed to be machine-generated. Record a translation as a draft only when its machine-draft origin is known. Review a string only after checking meaning, terminology, context, placeholders, and punctuation. The catalog check verifies that source and target placeholder tokens match for every reviewed or machine-draft value. The generated counts are in docs/localization/catalog-status.md.

To refresh source fingerprints after reviewing changed English copy, run yarn localization:update-source-hashes. To update the generated status table, run yarn localization:update-status. Then run yarn check-translations, yarn check-localization, and yarn test-localization.

## Terminology glossary

Use docs/localization/terminology.json for canonical product terms, meanings, translation guidance, and protected literal rules. Add or revise entries when product concepts change. Locale reviewers can record preferred target-language wording alongside a reviewed translation proposal; entries are checked against the English source catalog.

## Formatting and RTL

Use translatePlural with .zero, .one, .two, .few, .many, and .other suffix keys; Intl.PluralRules selects the locale category, with .other as the fallback. Use translateSelect with selector suffixes and an .other fallback. Format user-facing dates, numbers, and units with Utilities/Localization/formatters.cjs. Numeric form input is localized for display and parsing while its emitted JavaScript number remains canonical.

Document lang and dir come from the selected locale. The PostCSS logical-property pass rewrites physical left/right layout declarations. Text from books, authors, filenames, and imported metadata is isolated with <bdi dir="auto">; input values and API/path values are not transformed.

## Browser acceptance

Run the opt-in Playwright fixture against an isolated local instance with at least one seeded book:

    CHAPTARR_LOCALIZATION_BROWSER_URL=http://127.0.0.1:8787/ dotnet test src/Chaptarr.Core.Test/Chaptarr.Core.Test.csproj --filter TestCategory=LocalizationBrowser

The fixture switches through Arabic, Persian, and Hebrew in the UI, checks document direction, keyboard access to the language combobox, navigation, the books table, the unmapped/import screen, the keyboard-shortcuts dialog, mixed-direction input, and narrow 200% text layout. It restores the original UI language after the run. Use a disposable local database because the browser flow saves the UI language while it runs.
