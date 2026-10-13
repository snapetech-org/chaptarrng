# UI localization roadmap

## Catalog completion

ChaptarrNG keeps its `NzbDrone.Core/Localization/Core/*.json` catalogs, System > UI Language setting, and English fallback. This follow-up populates the Core catalogs for Spanish, Simplified Chinese, Hindi, German, Japanese, French, Brazilian Portuguese, Arabic, Korean, Traditional Chinese, Vietnamese, and Indonesian.

Each target catalog contains a non-empty value for all 2,295 English-source messages. This includes `AbsLibraryModeAudioOnly` and `AbsLibraryModeAudioAndEbooks` in all 12 locales; existing runtime language mappings are preserved.

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

The catalogs include application labels, help, accessibility text, dialogs, empty/error/success states, and visible workflow copy. Author and book metadata, filenames and paths, sample data, product names, commands, and protocol/API identifiers remain unchanged. English remains the fallback for future missing keys.

Matching upstream values were reused where available. Machine translation and curated common terms filled the remaining entries; some source-language values remain where a model result was unsafe or the text is a product or technical term. Static checks found full source-key coverage and no placeholder, ICU argument/type, rich-text tag, URL, inline-code, HTTP/HTTPS/URL-token, or plural-marker mismatches. These checks verify catalog structure, not translation quality. A phrase-by-phrase native-speaker review has not been completed, so wording and remaining English values need fluent review.

## RTL and follow-up

The Arabic strings are present, but full RTL layout acceptance remains open. Review and convert physical left/right layout rules before enabling global RTL direction, then verify common forms, tables, navigation, and modals at narrow and desktop widths.

1. Request fluent-speaker review for each target catalog, especially Arabic and Hindi.
2. Keep new Chaptarr-specific messages in the existing Core catalog and check source-key and placeholder parity in CI.
3. Preserve the current English fallback for any future missing message.
