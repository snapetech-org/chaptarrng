# UI localization roadmap

## Catalog completion

ChaptarrNG keeps its `NzbDrone.Core/Localization/Core/*.json` catalogs, System > UI Language setting, and English fallback. The expanded catalogs cover Spanish, Simplified Chinese, Hindi, German, Japanese, French, Brazilian Portuguese, Arabic, Korean, Traditional Chinese, Vietnamese, and Indonesian.

Each target catalog contains 2,230 non-empty entries, including `AbsLibraryModeAudioOnly` and `AbsLibraryModeAudioAndEbooks` in all 12 locales. The current English catalog has 65 keys that are absent from each translated catalog; `LocalizationService` falls back to English for those messages. Existing runtime language mappings are preserved.

The locale order remains:

1. Spanish (`es`)
2. Simplified Chinese (`zh-CN`)
3. Hindi (`hi`)
4. German (`de`)
5. Japanese (`ja`)
6. French (`fr`)
7. Brazilian Portuguese (`pt-BR`)
8. Arabic (`ar`)
9. Korean (`ko`)
10. Traditional Chinese (`zh-TW`)
11. Vietnamese (`vi`)
12. Indonesian (`id`)

## Translation scope and review

The catalogs include application labels, help, accessibility text, dialogs, empty/error/success states, and visible workflow copy. Author and book metadata, filenames and paths, sample data, product names, commands, and protocol/API identifiers remain unchanged. English remains the fallback for missing messages.

Matching upstream values were reused where available. Offline machine translation and curated common terms filled the remaining entries; some source-language values remain where a model result was unsafe or the text is a product or technical term. JSON validity and placeholder checks passed for the catalog entries present, and the frontend translation-key scan passed. These checks do not prove translation quality or full current-English key coverage. A phrase-by-phrase native-speaker review has not been completed, so wording and the 65 English-fallback messages in each catalog need review.

## RTL and follow-up

The Arabic strings are present, but full RTL layout acceptance remains open. Review and convert physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

1. Request fluent-speaker review for each target catalog, especially Arabic and Hindi.
2. Keep new Chaptarr-specific messages in the existing Core catalog and check source-key and placeholder parity in CI.
3. Preserve the current English fallback for any future missing message.
