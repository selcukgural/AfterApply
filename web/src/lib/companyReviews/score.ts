import type { CompanyReviewSummary } from "@/types/api";

/** "4.2" in every locale — the score is a number people compare, not prose. */
export function formatScore(score: number, locale: string): string {
  return new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(score);
}

/** "Eylül 2026" / "September 2026" from the month-precision string the API sends. */
export function formatSubmittedMonth(submittedMonth: string, locale: string): string {
  const [year, month] = submittedMonth.split("-").map(Number);
  if (!year || !month) return submittedMonth;
  return new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(year, month - 1, 1)),
  );
}

/** How many more approved reviews until a score is shown; 0 once it is. */
export function reviewsUntilScore(summary: Pick<CompanyReviewSummary, "approvedCount" | "minimumForScore">): number {
  return Math.max(0, summary.minimumForScore - summary.approvedCount);
}

/** The bar widths of the star distribution, as fractions of the largest bar (0..1). */
export function distributionFractions(distribution: readonly number[]): number[] {
  const max = Math.max(0, ...distribution);
  return distribution.map((count) => (max === 0 ? 0 : count / max));
}
