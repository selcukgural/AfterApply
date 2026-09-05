/**
 * Locale-aware number formatting for the dashboard.
 *
 * Rates arrive from the API already rounded to one decimal (AnalyticsCalculations.CalculateRate).
 * The dashboard used to run them back through Math.round(), which turned a real 0.4% interview
 * rate into "0%" — the single most misleading thing on the page for a high-volume applicant.
 * Everything here keeps that decimal.
 */

export function formatCount(value: number, locale: string): string {
  return new Intl.NumberFormat(locale).format(value);
}

/** `value` is a percentage the API already scaled to 0-100, not a 0-1 fraction. */
export function formatRate(value: number, locale: string): string {
  return new Intl.NumberFormat(locale, {
    style: "percent",
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value / 100);
}

export function formatDays(value: number, locale: string): string {
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: 1,
    maximumFractionDigits: 1,
  }).format(value);
}

/** Short label for a trend bucket, e.g. "8 Eyl" / "Sep 8". */
export function formatWeekStart(isoDate: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", timeZone: "UTC" }).format(
    new Date(`${isoDate}T00:00:00Z`),
  );
}
