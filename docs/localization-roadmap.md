# UI localization roadmap

## Catalog coverage

ChaptarrNG stores UI strings in the existing `NzbDrone.Core/Localization/Core/*.json` catalogs. The target locales are Spanish (`es`), Simplified Chinese (`zh_CN`), Hindi (`hi`), German (`de`), Japanese (`ja`), French (`fr`), Brazilian Portuguese (`pt_BR`), Arabic (`ar`), Korean (`ko`), Traditional Chinese (`zh_TW`), Vietnamese (`vi`), and Indonesian (`id`).

At the last full localization audit, all 2,705 English keys then present had non-empty values in the 12 target catalogs. The current catalogs have moved since that audit: as of 2026-10-20, `en.json` has 2,869 keys and each target catalog has 2,731 entries. Each target catalog is missing 142 current English keys and has four locale-only entries. Missing translations render through `LocalizationService.GetDictionary`'s English-source fallback. `SearchAsYouType`, series-pack search labels, and torrent-adoption labels are present in all 12 target catalogs.

## Validation and review

The last full validation checked JSON validity, duplicate keys, key parity, non-empty values, and placeholder, HTML-tag, and URL-token signatures for the 2,705-key audit baseline. A source scan checks long English clauses and unexpected writing systems; `yarn check-translations` checks frontend helper keys against `en.json`. That source check does not verify target-catalog parity. The follow-up work covers 142 current English keys missing from each target catalog, while retaining English fallback. Other work replaced hard-coded English UI in queue and history states, error and empty-state messages, search and import flows, deletion confirmations, settings and profile dialogs, notifications, help text, book-file counts, readable durations, and keyboard-shortcut descriptions. The bundle-recovery screen fetches Core strings when available and retains English fallback copy if localization cannot be loaded. The previous scan covered 1,603 source files and found no untranslated React JSX text.

Machine translation was used as a starting point for the catalog pass, followed by context-focused corrections and review. A fluent-speaker review of every phrase has not been completed; native-language review remains useful for idiom and tone.

Arabic strings are available, but full right-to-left layout acceptance remains open. Review physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

## Follow-up

1. Request fluent-speaker review for each target locale, prioritizing longer help text and settings labels.
2. Keep new ChaptarrNG messages in the existing Core catalogs and check key and placeholder parity in CI.
