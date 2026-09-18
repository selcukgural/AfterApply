import type { CvScanCategory, CvScanCategoryScore } from "@/types/api";

/**
 * The shared-score card: what a "share your score" link carries in its URL, and nothing else.
 *
 * `88-34-22-13-19` is the headline score followed by the four category subtotals in the order the
 * scan reports them; a bare `88` is a score alone. Numbers only, by design (growth audit
 * 2026-09-14, finding 03a; decided 2026-09-18): the scan stays anonymous, nothing about the file,
 * the text or the person is in the address, and a card is exactly as public as the person who
 * pasted it somewhere chose to make it. The category order and weights mirror
 * `CvScanScoring.Weights` on the API — a card whose parts do not add up to its score is refused
 * rather than drawn, so a hand-edited URL cannot put a number on the site's card that the scan
 * never produced.
 */

/** In report order. The weights are the API's (`CvScanScoring.Weights`); they sum to 100. */
export const SCORE_CARD_CATEGORIES: readonly { category: CvScanCategory; weight: number }[] = [
  { category: "MachineReadability", weight: 40 },
  { category: "SectionsAndDates", weight: 25 },
  { category: "Contact", weight: 15 },
  { category: "FormatAndLength", weight: 20 },
];

export interface ScoreCard {
  score: number;
  /** Null when the card carries the score alone. */
  categories: CvScanCategoryScore[] | null;
}

const CARD_PATTERN = /^(\d{1,3})(?:-(\d{1,2})-(\d{1,2})-(\d{1,2})-(\d{1,2}))?$/;

/** The URL segment for a result — always with the categories, since the result has them. */
export function formatScoreCard(score: number, categories: readonly CvScanCategoryScore[]): string {
  const parts = SCORE_CARD_CATEGORIES.map(
    ({ category }) => categories.find((entry) => entry.category === category)?.score ?? 0,
  );
  return [score, ...parts].join("-");
}

/** Reads a card back, or null when it is not one the scan could have produced. */
export function parseScoreCard(segment: string): ScoreCard | null {
  const match = CARD_PATTERN.exec(segment);
  if (!match) return null;

  const score = Number(match[1]);
  if (score > 100) return null;
  if (match[2] === undefined) return { score, categories: null };

  const categories = SCORE_CARD_CATEGORIES.map(({ category, weight }, index) => ({
    category,
    weight,
    score: Number(match[index + 2]),
  }));
  if (categories.some((entry) => entry.score > entry.weight)) return null;
  if (categories.reduce((sum, entry) => sum + entry.score, 0) !== score) return null;

  return { score, categories };
}
