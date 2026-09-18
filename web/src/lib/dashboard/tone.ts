/**
 * The two small rules that keep the dashboard from reading as a scoreboard of what did not
 * happen (DEVELOPMENT_PLAN.md, T-series, T1). Pure so the page's copy decisions are testable
 * without rendering it.
 */

export type ProgressKey = "both" | "interviews" | "offers";

/**
 * Which "in progress" sentence fits the headline, or null when nothing is in motion. The second
 * sentence only names what is actually happening: "0 offers awaiting a decision" is not news to
 * the person reading it — it is the one number they came hoping not to see.
 */
export function progressKey(interviews: number, offers: number): ProgressKey | null {
  if (interviews > 0 && offers > 0) return "both";
  if (interviews > 0) return "interviews";
  if (offers > 0) return "offers";
  return null;
}

/**
 * The rate chip only when there is a count behind it. "0 · 0% rate" is not information, it is the
 * same absence said twice — on exactly the tile a person least wants to look at.
 */
export function rateChip(count: number, chip: string): string | undefined {
  return count > 0 ? chip : undefined;
}
