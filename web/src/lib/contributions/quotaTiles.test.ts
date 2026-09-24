import { describe, expect, it } from "vitest";
import { buildContributionTiles } from "./quotaTiles";

describe("buildContributionTiles", () => {
  it("gives each kind with a quota a tile, in review → salary → experience order, linking to its contribute tab", () => {
    const tiles = buildContributionTiles({
      reviewQuota: { used: 3, limit: 10 },
      salaryQuota: { used: 1, limit: 10 },
      experienceQuota: { used: 1, limit: 10 },
    });
    expect(tiles.map((tile) => tile.kind)).toEqual(["review", "salary", "experience"]);
    expect(tiles.map((tile) => tile.href)).toEqual(["/contribute?tab=review", "/contribute?tab=salary", "/contribute?tab=experience"]);
    expect(tiles[0]).toMatchObject({ used: 3, limit: 10, full: false, percent: 30 });
  });

  it("drops a kind whose feature is off (no quota reported)", () => {
    const tiles = buildContributionTiles({ reviewQuota: { used: 0, limit: 10 }, salaryQuota: null, experienceQuota: { used: 2, limit: 10 } });
    expect(tiles.map((tile) => tile.kind)).toEqual(["review", "experience"]);
  });

  it("keeps a kind at its limit, marked full with a full bar", () => {
    const [review] = buildContributionTiles({ reviewQuota: { used: 10, limit: 10 }, salaryQuota: null, experienceQuota: null });
    expect(review).toMatchObject({ full: true, percent: 100 });
  });

  it("reads an admin override as the limit, including one lowered below what the author already has", () => {
    const [raised] = buildContributionTiles({ reviewQuota: { used: 12, limit: 25 }, salaryQuota: null, experienceQuota: null });
    expect(raised).toMatchObject({ full: false, percent: 48 });
    const [lowered] = buildContributionTiles({ reviewQuota: { used: 7, limit: 5 }, salaryQuota: null, experienceQuota: null });
    expect(lowered).toMatchObject({ full: true, percent: 100 });
  });

  it("treats a zero limit as full rather than dividing by zero", () => {
    const [review] = buildContributionTiles({ reviewQuota: { used: 0, limit: 0 }, salaryQuota: null, experienceQuota: null });
    expect(review).toMatchObject({ full: true, percent: 100 });
  });
});
