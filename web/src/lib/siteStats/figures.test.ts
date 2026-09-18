import { describe, expect, it } from "vitest";
import { visibleFigures } from "./figures";

describe("visibleFigures", () => {
  it("lists only the figures the API let through, in a fixed order", () => {
    expect(visibleFigures({ cvScans: 412, benchmarkAnswers: null, publishedReviews: 58 })).toEqual([
      { key: "cvScans", count: 412 },
      { key: "publishedReviews", count: 58 },
    ]);
  });

  it("is empty — so no strip is drawn — when nothing passed the threshold or the API was down", () => {
    expect(visibleFigures({ cvScans: null, benchmarkAnswers: null, publishedReviews: null })).toEqual([]);
    expect(visibleFigures(null)).toEqual([]);
  });
});
