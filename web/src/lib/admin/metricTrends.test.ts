import { describe, expect, it } from "vitest";
import type { ProductMetricsDayResponse } from "@/types/api";
import { latestPoint, nearestIndex, sevenDayChange, trendSeries } from "./metricTrends";

const day = (snapshotDate: string, totalUsers: number, d30: number | null = null) =>
  ({ snapshotDate, totalUsers, d30RetentionRate: d30 }) as ProductMetricsDayResponse;

describe("trendSeries", () => {
  it("orders the days oldest first, whatever order the API sent", () => {
    const series = trendSeries([day("2026-10-02", 33), day("2026-09-30", 32), day("2026-10-01", 33)], (d) => d.totalUsers);
    expect(series.map((p) => p.date)).toEqual(["2026-09-30", "2026-10-01", "2026-10-02"]);
  });

  it("keeps a missing value as null, not zero", () => {
    const series = trendSeries([day("2026-09-24", 31, null), day("2026-09-25", 31, 0)], (d) => d.d30RetentionRate);
    expect(series.map((p) => p.value)).toEqual([null, 0]);
  });
});

describe("latestPoint", () => {
  it("skips trailing days without a value", () => {
    expect(latestPoint([{ date: "a", value: 4 }, { date: "b", value: null }])).toEqual({ date: "a", value: 4 });
    expect(latestPoint([{ date: "a", value: null }])).toBeNull();
  });
});

describe("sevenDayChange", () => {
  const series = (values: (number | null)[]) => values.map((value, i) => ({ date: String(i), value }));

  it("compares the latest value with the one seven days earlier", () => {
    expect(sevenDayChange(series([25, 26, 27, 28, 29, 30, 31, 33]))).toBe(8);
  });

  it("has no answer with less than a week of history or a missing start", () => {
    expect(sevenDayChange(series([1, 2, 3]))).toBeNull();
    expect(sevenDayChange(series([null, 0, 0, 0, 0, 0, 0, 25]))).toBeNull();
  });
});

describe("nearestIndex", () => {
  it("snaps the pointer to the closest day and clamps at the edges", () => {
    expect(nearestIndex(0, 26)).toBe(0);
    expect(nearestIndex(1, 26)).toBe(25);
    expect(nearestIndex(0.5, 3)).toBe(1);
    expect(nearestIndex(-0.2, 10)).toBe(0);
    expect(nearestIndex(1.4, 10)).toBe(9);
    expect(nearestIndex(0.7, 1)).toBe(0);
  });
});
