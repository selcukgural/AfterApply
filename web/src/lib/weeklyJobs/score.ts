/**
 * How a fit score reads on the page. The bands are the ones the scoring prompt defines (90–100
 * every requirement, 70–89 the core ones, 50–69 a significant gap, below that a different field),
 * collapsed to the four semantic tones the design system has: good / accent / warn / crit. An
 * unscored posting gets the muted tone and no number.
 */
export type ScoreTone = "good" | "accent" | "warn" | "crit" | "none";

export function scoreTone(score: number | null | undefined): ScoreTone {
  if (score === null || score === undefined) return "none";
  if (score >= 80) return "good";
  if (score >= 60) return "accent";
  if (score >= 40) return "warn";
  return "crit";
}

/** Badge (wash background + ink text) classes per tone, from globals.css's semantic tokens. */
export const SCORE_BADGE_CLASSES: Record<ScoreTone, string> = {
  good: "bg-good-wash text-good-ink",
  accent: "bg-accent-wash text-accent-ink",
  warn: "bg-warn-wash text-warn-ink",
  crit: "bg-crit-wash text-crit-ink",
  none: "bg-muted-wash text-muted-ink",
};

/** Large-number text classes per tone (the detail header's "%92"). */
export const SCORE_TEXT_CLASSES: Record<ScoreTone, string> = {
  good: "text-good-ink",
  accent: "text-accent-ink",
  warn: "text-warn-ink",
  crit: "text-crit-ink",
  none: "text-muted-ink",
};

/** `yyyyWW` (ISO week key, as the API returns it) → the Monday that week starts on, UTC. */
export function weekKeyToMonday(weekKey: number): Date {
  const year = Math.floor(weekKey / 100);
  const week = weekKey % 100;
  // ISO week 1 is the week with 4 January in it; its Monday is 4 Jan minus (weekday - 1).
  const jan4 = new Date(Date.UTC(year, 0, 4));
  const jan4Weekday = jan4.getUTCDay() === 0 ? 7 : jan4.getUTCDay();
  const week1Monday = new Date(jan4);
  week1Monday.setUTCDate(jan4.getUTCDate() - (jan4Weekday - 1));
  const monday = new Date(week1Monday);
  monday.setUTCDate(week1Monday.getUTCDate() + (week - 1) * 7);
  return monday;
}

export function weekNumber(weekKey: number): number {
  return weekKey % 100;
}

/** The site's name as people write it; an unknown value comes back as given. */
export function sourceLabel(source: string): string {
  switch (source) {
    case "LinkedIn":
      return "LinkedIn";
    case "KariyerNet":
      return "kariyer.net";
    default:
      return source;
  }
}
