import { describe, expect, it } from "vitest";
import type { BenchmarkResultResponse } from "@/types/api";
import { surveyProgress } from "./survey";

function result(overrides: Partial<BenchmarkResultResponse>): BenchmarkResultResponse {
  return {
    sector: "SoftwareAndIt",
    applicationCount: 60,
    replyCount: 12,
    yourRate: 20,
    sampleSize: 4,
    totalSubmissions: 6,
    minimumSampleSize: 30,
    scope: "None",
    comparedAgainstCount: null,
    medianRate: null,
    shareBelowYou: null,
    ...overrides,
  };
}

describe("surveyProgress", () => {
  it("counts both steps from what the API returned while nothing can be compared", () => {
    const progress = surveyProgress(result({}))!;

    expect(progress.participant).toBe(6);
    expect(progress.overall).toEqual({ count: 6, minimum: 30, reached: false, percent: 20 });
    expect(progress.sector).toEqual({ count: 4, minimum: 30, reached: false, percent: 13 });
  });

  it("marks the first step reached once the overall median is showing", () => {
    const progress = surveyProgress(
      result({ scope: "Overall", totalSubmissions: 41, sampleSize: 18, medianRate: 22, comparedAgainstCount: 41 }),
    )!;

    expect(progress.overall.reached).toBe(true);
    // Capped: 41 of 30 is a full bar, not an overflowing one.
    expect(progress.overall.percent).toBe(100);
    expect(progress.sector.reached).toBe(false);
    expect(progress.sector.percent).toBe(60);
  });

  it("steps aside once the sector stands on its own", () => {
    expect(surveyProgress(result({ scope: "Sector", sampleSize: 30, totalSubmissions: 55, medianRate: 25 }))).toBeNull();
  });
});
