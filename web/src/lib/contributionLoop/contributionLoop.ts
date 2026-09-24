/**
 * The contribution loop's small arithmetic (2026-09-24): how long ago a process ended, in the
 * words the ended-processes card uses, and where an amount sits on a salary band's bar.
 */

const DAY_MS = 86_400_000;

/** "5 weeks ago" up to eight weeks, "3 months ago" after — the card never shows a date: a day
 *  would be more exact than anything the rating itself will ever say about when it happened. */
export function elapsedSince(endedAt: string, now: Date): { unit: "week" | "month"; count: number } {
  const days = Math.max(0, Math.floor((now.getTime() - new Date(endedAt).getTime()) / DAY_MS));
  if (days < 56) return { unit: "week", count: Math.max(1, Math.floor(days / 7)) };
  return { unit: "month", count: Math.max(2, Math.floor(days / 30)) };
}

/** Where `value` falls between `min` and `max`, as a percentage of the bar, clamped to its ends
 *  (an own amount outside the current band — an old salary — sits at the edge, not off it). */
export function bandOffset(value: number, min: number, max: number): number {
  if (max <= min) return 50;
  return Math.min(100, Math.max(0, ((value - min) / (max - min)) * 100));
}

/** Which sentence describes the distance from the median. */
export function positionKey(percentFromMedian: number | null): "above" | "below" | "equal" | null {
  if (percentFromMedian === null) return null;
  if (percentFromMedian > 0) return "above";
  if (percentFromMedian < 0) return "below";
  return "equal";
}
