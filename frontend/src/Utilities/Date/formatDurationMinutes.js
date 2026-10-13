function formatDurationMinutes(minutes) {
  if (!minutes || minutes <= 0) {
    return '';
  }

  const totalMinutes = Math.round(minutes);
  const hours = Math.floor(totalMinutes / 60);
  const mins = totalMinutes % 60;
  const locale = typeof document !== 'undefined' ? document.documentElement.lang : undefined;
  const formatUnit = (value, unit) => new Intl.NumberFormat(locale, {
    style: 'unit',
    unit,
    unitDisplay: 'narrow'
  }).format(value);

  if (hours > 0 && mins > 0) {
    return `${formatUnit(hours, 'hour')} ${formatUnit(mins, 'minute')}`;
  }

  if (hours > 0) {
    return formatUnit(hours, 'hour');
  }

  return formatUnit(mins, 'minute');
}

export default formatDurationMinutes;
