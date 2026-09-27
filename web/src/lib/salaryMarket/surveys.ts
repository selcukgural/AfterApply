import type { SalarySurveyEdition } from "@/types/api";

/** The first and last survey year behind a set of figures. */
export function surveyYears(editions: readonly SalarySurveyEdition[]): { first: number; last: number } {
  const years = editions.map((e) => e.year);
  return years.length === 0 ? { first: 0, last: 0 } : { first: Math.min(...years), last: Math.max(...years) };
}

/** Each survey author once, pointing at their profile (a year's repository's parent): the
 *  Dataset's creators and the sources box name the same people. */
export function surveyCreators(editions: readonly SalarySurveyEdition[]): { name: string; url: string }[] {
  const seen = new Map<string, { name: string; url: string }>();
  for (const edition of editions) {
    if (!seen.has(edition.sourceCode)) {
      seen.set(edition.sourceCode, { name: edition.sourceName, url: edition.sourceUrl.replace(/\/[^/]+$/, "") });
    }
  }
  return [...seen.values()];
}
