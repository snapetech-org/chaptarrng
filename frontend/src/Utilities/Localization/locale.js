const localeRegistry = require('../../../../src/NzbDrone.Core/Localization/Core/ui-locales.json');
const localeUtils = require('./locale-utils.cjs');

export const SUPPORTED_UI_LOCALES = Object.freeze(
  localeRegistry.locales.map((locale) => Object.freeze(locale))
);
export const SUPPORTED_UI_LOCALE_IDS = new Set(SUPPORTED_UI_LOCALES.map((locale) => locale.id));

const uiLocaleById = new Map(SUPPORTED_UI_LOCALES.map((locale) => [locale.id, locale]));

export function getUiLocaleById(id) {
  return uiLocaleById.get(Number(id));
}

export const normalizeLocaleTag = localeUtils.normalizeLocaleTag;
export const getDirectionForLocale = localeUtils.getDirectionForLocale;
export const applyDocumentLocale = localeUtils.applyDocumentLocale;
export const getLocale = localeUtils.getCurrentLocale;
