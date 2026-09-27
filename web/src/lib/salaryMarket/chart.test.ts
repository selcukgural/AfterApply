import { describe, expect, it } from "vitest";
import type { SalaryStats } from "@/types/api";
import {
  bandPath,
  formatChange,
  formatLira,
  formatStat,
  linePath,
  percentChange,
  rangeBar,
  sparkline,
  valueAxis,
  xAt,
  yAt,
} from "./chart";

const stats = (overrides: Partial<SalaryStats> = {}): SalaryStats => ({
  count: 40, p10: 68_500, p25: 97_500, p50: 132_500, p75: 177_500, p90: 215_000, atLeast: [], ...overrides,
});

describe("money", () => {
  it("writes lira the way each language does", () => {
    expect(formatLira(132_500, "tr")).toBe("132.500 ₺");
    expect(formatLira(132_500, "en")).toBe("₺132,500");
  });

  it("marks a figure that is only a floor", () => {
    expect(formatLira(15_000, "tr", true)).toBe("≥ 15.000 ₺");
    expect(formatStat(stats({ p90: 15_000, atLeast: ["P90"] }), "p90", "tr")).toBe("≥ 15.000 ₺");
    expect(formatStat(stats({ atLeast: ["P90"] }), "p75", "tr")).toBe("177.500 ₺");
  });
});

describe("change", () => {
  it("is whole percent from the year before", () => {
    expect(percentChange(132_500, 102_500)).toBe(29);
    expect(percentChange(90, 100)).toBe(-10);
  });

  it("is nothing without a year before", () => {
    expect(percentChange(132_500, null)).toBeNull();
    expect(percentChange(132_500, undefined)).toBeNull();
    expect(percentChange(132_500, 0)).toBeNull();
  });

  it("is written with the percent sign where each language puts it", () => {
    expect(formatChange(29, "tr")).toBe("+%29");
    expect(formatChange(29, "en")).toBe("+29%");
    expect(formatChange(-10, "tr")).toBe("−%10");
    expect(formatChange(0, "en")).toBe("0%");
  });
});

describe("axis", () => {
  it("ends on a round number just above the largest value", () => {
    expect(valueAxis(177_500)).toEqual({ max: 200_000, ticks: [0, 50_000, 100_000, 150_000, 200_000] });
    expect(valueAxis(9_000).max).toBe(10_000);
  });

  it("survives an empty chart", () => {
    expect(valueAxis(0)).toEqual({ max: 1, ticks: [0] });
  });
});

describe("geometry", () => {
  const plot = { width: 1000, height: 300, left: 60, right: 140, top: 10, bottom: 30 };

  it("spreads the years across the plot and puts zero on the floor", () => {
    expect(xAt(plot, 0, 9)).toBe(60);
    expect(xAt(plot, 8, 9)).toBe(860);
    expect(yAt(plot, 0, 200_000)).toBe(270);
    expect(yAt(plot, 200_000, 200_000)).toBe(10);
  });

  it("draws lines and closed bands", () => {
    const upper = [{ x: 0, y: 10 }, { x: 10, y: 5 }];
    const lower = [{ x: 0, y: 20 }, { x: 10, y: 15 }];
    expect(linePath(upper)).toBe("M0.0 10.0 L10.0 5.0");
    expect(bandPath(upper, lower)).toBe("M0.0 10.0 L10.0 5.0 L10.0 15.0 L0.0 20.0 Z");
    expect(bandPath([], [])).toBe("");
  });

  it("keeps a sparkline's years in fixed columns, gaps included", () => {
    const years = [2018, 2019, 2020, 2021, 2022, 2023, 2024, 2025, 2026];
    const line = sparkline([{ year: 2025, p50: 50 }, { year: 2026, p50: 100 }], years, { width: 132, height: 30, pad: 6 });

    expect(line.points.map((p) => p.x)).toEqual([6 + 7 * 15, 6 + 8 * 15]);
    expect(line.points[1].y).toBe(6);
    expect(line.points[0].y).toBe(15);
  });

  it("places a range bar in percent and clamps it", () => {
    expect(rangeBar({ p25: 50, p50: 100, p75: 150 }, 200)).toEqual({ left: 25, width: 50, median: 50 });
    expect(rangeBar({ p25: 150, p50: 250, p75: 300 }, 200)).toEqual({ left: 75, width: 25, median: 100 });
  });
});
