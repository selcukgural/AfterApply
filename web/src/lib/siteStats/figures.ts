import type { SiteStatsResponse } from "@/types/api";

export type SiteStatFigure = { key: "cvScans" | "benchmarkAnswers" | "publishedReviews"; count: number };

/**
 * The figures a page may show, in a fixed order, from a stats response that has already had the
 * threshold applied by the API (a withheld figure arrives as null). Pure, so the "nothing to
 * show" case — the page draws no strip at all — is tested without a fetch.
 */
export function visibleFigures(stats: SiteStatsResponse | null): SiteStatFigure[] {
  if (!stats) return [];
  const figures: SiteStatFigure[] = [];
  if (stats.cvScans !== null) figures.push({ key: "cvScans", count: stats.cvScans });
  if (stats.benchmarkAnswers !== null) figures.push({ key: "benchmarkAnswers", count: stats.benchmarkAnswers });
  if (stats.publishedReviews !== null) figures.push({ key: "publishedReviews", count: stats.publishedReviews });
  return figures;
}
