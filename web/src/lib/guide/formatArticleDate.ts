/**
 * "8 Eylül 2026" / "8 September 2026".
 *
 * `Intl` rather than a hand-rolled month table, and a fixed UTC time zone: the published dates are
 * plain calendar dates ("2026-09-08"), and letting the server's zone shift them would print the
 * previous day for anyone west of UTC.
 */
export function formatArticleDate(isoDate: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, {
    day: "numeric",
    month: "long",
    year: "numeric",
    timeZone: "UTC",
  }).format(new Date(`${isoDate}T00:00:00Z`));
}
