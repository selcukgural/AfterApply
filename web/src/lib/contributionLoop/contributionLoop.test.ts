import { describe, expect, it } from "vitest";
import { bandOffset, elapsedSince, positionKey } from "./contributionLoop";

const now = new Date("2026-09-24T12:00:00Z");
const daysAgo = (days: number) => new Date(now.getTime() - days * 86_400_000).toISOString();

describe("elapsedSince", () => {
  it("counts whole weeks up to eight weeks", () => {
    expect(elapsedSince(daysAgo(28), now)).toEqual({ unit: "week", count: 4 });
    expect(elapsedSince(daysAgo(36), now)).toEqual({ unit: "week", count: 5 });
    expect(elapsedSince(daysAgo(55), now)).toEqual({ unit: "week", count: 7 });
  });

  it("switches to months from eight weeks on", () => {
    expect(elapsedSince(daysAgo(56), now)).toEqual({ unit: "month", count: 2 });
    expect(elapsedSince(daysAgo(95), now)).toEqual({ unit: "month", count: 3 });
    expect(elapsedSince(daysAgo(364), now)).toEqual({ unit: "month", count: 12 });
  });

  it("never says zero", () => {
    expect(elapsedSince(daysAgo(0), now)).toEqual({ unit: "week", count: 1 });
  });
});

describe("bandOffset", () => {
  it("places an amount on the bar and keeps it on the bar", () => {
    expect(bandOffset(95_000, 62_000, 140_000)).toBeCloseTo(42.31, 1);
    expect(bandOffset(62_000, 62_000, 140_000)).toBe(0);
    expect(bandOffset(40_000, 62_000, 140_000)).toBe(0);
    expect(bandOffset(200_000, 62_000, 140_000)).toBe(100);
  });

  it("centres a band with no width", () => {
    expect(bandOffset(80_000, 80_000, 80_000)).toBe(50);
  });
});

describe("positionKey", () => {
  it("names the side of the median, and says nothing without a band", () => {
    expect(positionKey(8)).toBe("above");
    expect(positionKey(-12)).toBe("below");
    expect(positionKey(0)).toBe("equal");
    expect(positionKey(null)).toBeNull();
  });
});
