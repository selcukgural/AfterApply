import { describe, expect, it } from "vitest";
import { splitDistribution, summariseOutcome } from "@/lib/dashboard/statusGroups";
import type { StatusDistributionItem } from "@/types/api";

describe("splitDistribution", () => {
  it("pulls a bucket out of scale when it would flatten every other bar", () => {
    // The screenshot that started the redesign: 1,177 Applied against ten other records.
    const split = splitDistribution([
      { status: "Applied", count: 1177 },
      { status: "Interview", count: 3 },
      { status: "Rejected", count: 3 },
      { status: "Offer", count: 1 },
      { status: "Ghosted", count: 1 },
    ]);

    expect(split.dominant?.status).toBe("Applied");
    expect(split.rest.map((item) => item.count)).toEqual([3, 3, 1, 1]);
    expect(split.restMax).toBe(3);
  });

  it("keeps one shared scale when no bucket dominates", () => {
    const split = splitDistribution([
      { status: "Applied", count: 40 },
      { status: "Rejected", count: 25 },
      { status: "Interview", count: 18 },
    ]);

    expect(split.dominant).toBeNull();
    expect(split.rest).toHaveLength(3);
    expect(split.restMax).toBe(40);
  });

  it("sorts descending and drops empty statuses", () => {
    const split = splitDistribution([
      { status: "Screening", count: 0 },
      { status: "Interview", count: 2 },
      { status: "Rejected", count: 7 },
    ]);

    expect(split.rest.map((item) => item.status)).toEqual(["Rejected", "Interview"]);
  });

  it("never treats a lone bucket as dominant — there is nothing for it to flatten", () => {
    const split = splitDistribution([{ status: "Applied", count: 900 }]);

    expect(split.dominant).toBeNull();
    expect(split.restMax).toBe(900);
  });

  it("returns an empty split for an all-zero distribution", () => {
    const empty: StatusDistributionItem[] = [
      { status: "Applied", count: 0 },
      { status: "Offer", count: 0 },
    ];

    expect(splitDistribution(empty)).toEqual({ dominant: null, rest: [], restMax: 0 });
  });
});

describe("summariseOutcome", () => {
  it("counts an offer in hand as a win, alongside a formal acceptance", () => {
    const outcome = summariseOutcome([
      { status: "Offer", count: 1 },
      { status: "Accepted", count: 2 },
      { status: "Rejected", count: 3 },
      { status: "Ghosted", count: 1 },
      { status: "Withdrawn", count: 1 },
    ]);

    expect(outcome.won).toBe(3);
    expect(outcome.resolved).toBe(8);
    expect(outcome.winRate).toBeCloseTo(37.5, 5);
  });

  it("excludes applications still in flight from the resolved total", () => {
    const outcome = summariseOutcome([
      { status: "Applied", count: 1177 },
      { status: "Interview", count: 3 },
      { status: "Rejected", count: 3 },
    ]);

    expect(outcome.resolved).toBe(3);
    expect(outcome.winRate).toBe(0);
  });

  it("reports a zero win rate rather than dividing by nothing", () => {
    const outcome = summariseOutcome([{ status: "Applied", count: 5 }]);

    expect(outcome).toMatchObject({ won: 0, lost: 0, resolved: 0, winRate: 0 });
  });
});
