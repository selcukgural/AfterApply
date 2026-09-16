import { describe, expect, it } from "vitest";
import { scoreTone, sourceLabel, weekKeyToMonday, weekNumber } from "./score";

describe("scoreTone", () => {
  it("bands the score the way the scoring prompt defines it", () => {
    expect(scoreTone(100)).toBe("good");
    expect(scoreTone(80)).toBe("good");
    expect(scoreTone(79)).toBe("accent");
    expect(scoreTone(60)).toBe("accent");
    expect(scoreTone(59)).toBe("warn");
    expect(scoreTone(40)).toBe("warn");
    expect(scoreTone(39)).toBe("crit");
    expect(scoreTone(0)).toBe("crit");
  });

  it("gives an unscored posting the muted tone", () => {
    expect(scoreTone(null)).toBe("none");
    expect(scoreTone(undefined)).toBe("none");
  });
});

describe("weekKeyToMonday", () => {
  it("finds the Monday of an ISO week", () => {
    // 2026-W38 starts Monday 14 September 2026 — the sweep that produced this feature.
    expect(weekKeyToMonday(202638).toISOString()).toBe("2026-09-14T00:00:00.000Z");
    // Week 1 of 2027 starts on 4 January 2027 (a Monday).
    expect(weekKeyToMonday(202701).toISOString()).toBe("2027-01-04T00:00:00.000Z");
    // Week 1 of 2026 starts in the previous year: Monday 29 December 2025.
    expect(weekKeyToMonday(202601).toISOString()).toBe("2025-12-29T00:00:00.000Z");
  });

  it("reads the week number off the key", () => {
    expect(weekNumber(202638)).toBe(38);
  });
});

describe("sourceLabel", () => {
  it("names the sites the way people write them", () => {
    expect(sourceLabel("LinkedIn")).toBe("LinkedIn");
    expect(sourceLabel("KariyerNet")).toBe("kariyer.net");
    expect(sourceLabel("Other")).toBe("Other");
  });
});
