import { describe, expect, it } from "vitest";
import { distributionFractions, formatScore, formatSubmittedMonth, reviewsUntilScore } from "./score";

describe("score display helpers", () => {
  it("formats the score with one decimal in both locales", () => {
    expect(formatScore(4, "en")).toBe("4.0");
    expect(formatScore(3.94, "tr")).toBe("3,9");
  });

  it("renders the month-precision date as a month name", () => {
    expect(formatSubmittedMonth("2026-09", "en")).toBe("September 2026");
    expect(formatSubmittedMonth("2026-09", "tr")).toBe("Eylül 2026");
    expect(formatSubmittedMonth("garbage", "en")).toBe("garbage");
  });

  it("counts down to the score threshold and stops at zero", () => {
    expect(reviewsUntilScore({ approvedCount: 1, minimumForScore: 3 })).toBe(2);
    expect(reviewsUntilScore({ approvedCount: 5, minimumForScore: 3 })).toBe(0);
  });

  it("scales the distribution to the tallest bar", () => {
    expect(distributionFractions([0, 1, 2, 4, 2])).toEqual([0, 0.25, 0.5, 1, 0.5]);
    expect(distributionFractions([0, 0, 0, 0, 0])).toEqual([0, 0, 0, 0, 0]);
  });
});
