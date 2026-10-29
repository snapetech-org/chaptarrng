import intlFormatters from 'Utilities/Localization/formatters.cjs';
import { getLocale } from 'Utilities/Localization/locale';
import translate from 'Utilities/String/translate';

const { formatNumber, pluralCategory } = intlFormatters;

function formatAge(age, ageHours, ageMinutes) {
  age = Math.round(age);
  ageHours = parseFloat(ageHours);
  ageMinutes = ageMinutes && parseFloat(ageMinutes);

  if (age < 2 && ageHours) {
    if (ageHours < 2 && !!ageMinutes) {
      const minutes = Number(ageMinutes.toFixed(0));
      const unit = pluralCategory(minutes, getLocale()) === 'one' ?
        translate('FormatAgeMinute') :
        translate('FormatAgeMinutes');

      return `${formatNumber(minutes, { maximumFractionDigits: 0 }, getLocale())} ${unit}`;
    }

    const hours = Number(ageHours.toFixed(1));
    const unit = pluralCategory(hours, getLocale()) === 'one' ?
      translate('FormatAgeHour') :
      translate('FormatAgeHours');

    return `${formatNumber(hours, {
      minimumFractionDigits: 1,
      maximumFractionDigits: 1
    }, getLocale())} ${unit}`;
  }

  const unit = pluralCategory(age, getLocale()) === 'one' ?
    translate('FormatAgeDay') :
    translate('FormatAgeDays');

  return `${formatNumber(age, { maximumFractionDigits: 0 }, getLocale())} ${unit}`;
}

export default formatAge;
