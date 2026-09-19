import { scoreBand, type ScoreBand } from "@/lib/cvScan/findings";

/**
 * The arithmetic behind the category bars (2026-09-19): which band a subtotal falls in, how wide
 * its bar is, and what the number next to it reads while the bar is still growing. Kept out of
 * the component for the same reason as findings.ts — it is tested as numbers, not as a tree.
 *
 * The rule that carries over unchanged: **the bar is the same number again.** Its width is the
 * subtotal over the category's own weight, the colour is the band that ratio falls in (the
 * headline's thresholds, applied to the category), and the count-up only decides which frame of
 * that number is showing — never a different one. Once the motion is over, or when the reader
 * has asked for none, every value here equals the response.
 */

/** How long one bar takes to reach its width, and how far apart consecutive bars start. */
export const BAR_GROW_MS = 900;
export const BAR_STAGGER_MS = 140;

/** The headline's bands, applied to one category: 32/40 is "good" because 80/100 would be. */
export function categoryBand(score: number, weight: number): ScoreBand {
  return scoreBand(categoryFillPercent(score, weight));
}

/** Bar width in percent, clamped so a malformed response cannot draw past the track. */
export function categoryFillPercent(score: number, weight: number): number {
  if (weight <= 0) return 0;
  return Math.max(0, Math.min(100, (score / weight) * 100));
}

export function barDelayMs(index: number): number {
  return index * BAR_STAGGER_MS;
}

/** When the last bar has stopped: the whole sequence is over at this many ms. */
export function barsTotalMs(count: number): number {
  return count === 0 ? 0 : barDelayMs(count - 1) + BAR_GROW_MS;
}

/** The same curve the CSS animation uses (`cubic-bezier(0.2, 0.8, 0.2, 1)` is close to it), so the
 *  number and its bar arrive together rather than the number lagging behind. */
export function easeOutCubic(t: number): number {
  const clamped = Math.max(0, Math.min(1, t));
  return 1 - Math.pow(1 - clamped, 3);
}

/** How far along bar `index` is at `elapsedMs`, 0 before its turn and 1 once it has finished. */
export function barProgress(index: number, elapsedMs: number): number {
  return easeOutCubic((elapsedMs - barDelayMs(index)) / BAR_GROW_MS);
}

/** The subtotal to print next to bar `index` at `elapsedMs`: rounds to whole points because the
 *  response only ever has whole points, and lands exactly on `score` at the end. */
export function countUpValue(score: number, index: number, elapsedMs: number): number {
  return Math.round(score * barProgress(index, elapsedMs));
}
