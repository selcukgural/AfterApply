/**
 * The day a CV was uploaded, as the label that tells two same-named files apart ("12 Eylül",
 * "12 Eylül 2025"): the year only when it is not this year, since a CV version is nearly always
 * recent and the year would be noise on every one of them.
 */
export function cvVersionDate(iso: string, locale: string, now: Date = new Date()): string {
  const date = new Date(iso);
  return date.toLocaleDateString(locale, {
    day: "numeric",
    month: "long",
    ...(date.getFullYear() === now.getFullYear() ? {} : { year: "numeric" }),
  });
}
