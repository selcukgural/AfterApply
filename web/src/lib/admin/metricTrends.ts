import type { ProductMetricsDayResponse } from "@/types/api";

export interface TrendPoint {
  /** The snapshot day, `YYYY-MM-DD`. */
  date: string;
  /** Null where the metric had no value that day (a retention cohort not old enough yet). */
  value: number | null;
}

/**
 * One metric's history, oldest day first — the order a line is drawn in. The API sends the days
 * newest first, which is the order the table reads in.
 */
export function trendSeries(
  days: readonly ProductMetricsDayResponse[],
  pick: (day: ProductMetricsDayResponse) => number | null,
): TrendPoint[] {
  return [...days]
    .sort((a, b) => a.snapshotDate.localeCompare(b.snapshotDate))
    .map((day) => ({ date: day.snapshotDate, value: pick(day) }));
}

/** The last day that has a value, or null when the metric never had one in the window. */
export function latestPoint(series: readonly TrendPoint[]): TrendPoint | null {
  for (let i = series.length - 1; i >= 0; i--) {
    if (series[i].value !== null) {
      return series[i];
    }
  }
  return null;
}

/**
 * The change over the last seven days: the latest value against the value seven days before it.
 * Null when either end is missing — "no change" and "nothing to compare with" are different
 * answers, and a rate whose cohort only appeared this week has the second one.
 */
export function sevenDayChange(series: readonly TrendPoint[]): number | null {
  const lastIndex = series.findLastIndex((point) => point.value !== null);
  if (lastIndex < 7) {
    return null;
  }
  const latest = series[lastIndex].value;
  const before = series[lastIndex - 7].value;
  return latest === null || before === null ? null : latest - before;
}

/**
 * The day under the pointer: the chart's whole width is the hover target, and the pointer snaps to
 * the nearest day rather than having to land on a dot a few pixels wide.
 */
export function nearestIndex(fraction: number, length: number): number {
  if (length <= 1) {
    return 0;
  }
  const clamped = Math.min(1, Math.max(0, fraction));
  return Math.round(clamped * (length - 1));
}
