import type { SalaryStats } from "@/types/api";

/**
 * The salary pages' arithmetic, kept out of the components so it can be tested: money formatting,
 * year-on-year change, and the geometry of the trend chart, the sparklines and the range bars
 * (design canvas 2026-09-27, variants "Liste A" and "Detay A").
 */

export type Percentile = "p10" | "p25" | "p50" | "p75" | "p90";

/** "132.500 ₺" in Turkish, "₺132,500" in English; "≥ 15.000 ₺" for a figure that is only a floor. */
export function formatLira(value: number, locale: string, atLeast = false): string {
  const number = new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB", { maximumFractionDigits: 0 }).format(value);
  const money = locale === "tr" ? `${number} ₺` : `₺${number}`;
  return atLeast ? `≥ ${money}` : money;
}

/** Whether a percentile of these stats is only known as a floor. */
export function isAtLeast(stats: SalaryStats, percentile: Percentile): boolean {
  return stats.atLeast.includes(percentile.toUpperCase() as SalaryStats["atLeast"][number]);
}

export function formatStat(stats: SalaryStats, percentile: Percentile, locale: string): string {
  return formatLira(stats[percentile], locale, isAtLeast(stats, percentile));
}

/** Whole-percent change from `previous` to `current`, or null when there is no year before. */
export function percentChange(current: number, previous: number | null | undefined): number | null {
  if (previous === null || previous === undefined || previous <= 0) return null;
  return Math.round((current / previous - 1) * 100);
}

/** "+%29" in Turkish, "+29%" in English; a fall keeps its minus sign. */
export function formatChange(change: number, locale: string): string {
  const sign = change > 0 ? "+" : change < 0 ? "−" : "";
  return locale === "tr" ? `${sign}%${Math.abs(change)}` : `${sign}${Math.abs(change)}%`;
}

/** A value axis that starts at zero and ends on a round number just above `max`, with its ticks. */
export function valueAxis(max: number, tickCount = 4): { max: number; ticks: number[] } {
  if (max <= 0) return { max: 1, ticks: [0] };
  const rough = max / tickCount;
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 2.5, 5, 10].map((m) => m * magnitude).find((candidate) => candidate >= rough) ?? 10 * magnitude;
  const top = Math.ceil(max / step) * step;
  const ticks: number[] = [];
  for (let value = 0; value <= top + step / 2; value += step) ticks.push(value);
  return { max: top, ticks };
}

export interface Plot {
  width: number;
  height: number;
  left: number;
  right: number;
  top: number;
  bottom: number;
}

/** x of the i-th of `count` evenly spaced points inside the plot. */
export function xAt(plot: Plot, index: number, count: number): number {
  const span = plot.width - plot.left - plot.right;
  return count <= 1 ? plot.left + span / 2 : plot.left + (span * index) / (count - 1);
}

/** y of a value on an axis from 0 (bottom) to `max` (top). */
export function yAt(plot: Plot, value: number, max: number): number {
  const span = plot.height - plot.top - plot.bottom;
  return plot.top + span - (Math.max(0, value) / max) * span;
}

export type Point = { x: number; y: number };

export function linePath(points: readonly Point[]): string {
  return points.map((p, i) => `${i === 0 ? "M" : "L"}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ");
}

/** A closed band between an upper and a lower line drawn over the same x positions. */
export function bandPath(upper: readonly Point[], lower: readonly Point[]): string {
  if (upper.length === 0) return "";
  const back = [...lower].reverse();
  return `${linePath(upper)} ${back.map((p) => `L${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(" ")} Z`;
}

/**
 * A sparkline over a fixed run of years: a year with no figure is a gap in the x positions, not
 * a squeeze — so every row's 2026 sits in the same column. Each line uses its own scale: it shows
 * the shape of one occupation's history, the table's own column carries the amounts.
 */
export function sparkline(
  trend: readonly { year: number; p50: number }[],
  years: readonly number[],
  size: { width: number; height: number; pad: number },
): { path: string; points: (Point & { year: number; p50: number })[] } {
  const max = Math.max(...trend.map((t) => t.p50), 1);
  const step = years.length > 1 ? (size.width - size.pad * 2) / (years.length - 1) : 0;
  const points = trend
    .filter((t) => years.includes(t.year))
    .map((t) => ({
      year: t.year,
      p50: t.p50,
      x: size.pad + years.indexOf(t.year) * step,
      y: size.height - size.pad - (t.p50 / max) * (size.height - size.pad * 2),
    }));
  return { path: linePath(points), points };
}

/** Where a range sits on a bar running from 0 to `max`, as percentages for CSS. */
export function rangeBar(stats: Pick<SalaryStats, "p25" | "p50" | "p75">, max: number): { left: number; width: number; median: number } {
  const pct = (value: number) => Math.min(100, Math.max(0, (value / max) * 100));
  return { left: pct(stats.p25), width: pct(stats.p75) - pct(stats.p25), median: pct(stats.p50) };
}
