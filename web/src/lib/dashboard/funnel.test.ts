import { describe, expect, it } from "vitest";
import { buildFunnel, findBottleneck } from "@/lib/dashboard/funnel";
import type { AnalyticsRatesResponse } from "@/types/api";

function rates(overrides: Partial<AnalyticsRatesResponse> = {}): AnalyticsRatesResponse {
  return {
    totalApplications: 0,
    respondedCount: 0,
    responseRate: 0,
    interviewCount: 0,
    interviewRate: 0,
    offerCount: 0,
    offerRate: 0,
    rejectedCount: 0,
    rejectionRate: 0,
    ghostedCount: 0,
    ghostingRate: 0,
    ...overrides,
  };
}

describe("buildFunnel", () => {
  it("leaves the first stage without a conversion, since it has no predecessor", () => {
    const stages = buildFunnel(rates({ totalApplications: 10 }));

    expect(stages[0]).toEqual({ key: "applied", count: 10, conversion: null });
  });

  it("computes each stage against the stage above it, not against the total", () => {
    const stages = buildFunnel(
      rates({ totalApplications: 200, respondedCount: 20, interviewCount: 10, offerCount: 2 }),
    );

    expect(stages.map((stage) => stage.conversion)).toEqual([null, 10, 50, 20]);
  });

  it("keeps sub-1% conversions instead of collapsing them to zero", () => {
    // The real shape of a bulk-import account: 1,187 applications, 10 replies.
    const stages = buildFunnel(rates({ totalApplications: 1187, respondedCount: 10 }));

    expect(stages[1].conversion).toBeCloseTo(0.842, 3);
  });

  it("reports zero rather than dividing by an empty stage", () => {
    const stages = buildFunnel(rates({ totalApplications: 0, respondedCount: 0 }));

    expect(stages.every((stage) => stage.conversion === null || stage.conversion === 0)).toBe(true);
  });

  it("can exceed 100 when a stage was skipped", () => {
    // Screening straight to Offer records an offer that never passed an interview status.
    const stages = buildFunnel(
      rates({ totalApplications: 10, respondedCount: 4, interviewCount: 1, offerCount: 2 }),
    );

    expect(stages[3].conversion).toBe(200);
  });
});

describe("findBottleneck", () => {
  it("names the stage with the steepest drop-off", () => {
    const stages = buildFunnel(
      rates({ totalApplications: 200, respondedCount: 20, interviewCount: 10, offerCount: 2 }),
    );

    expect(findBottleneck(stages)?.key).toBe("responded");
  });

  it("ignores stages whose predecessor was already empty", () => {
    // Nothing ever got a reply, so "interview" and "offer" are 0-of-0, not real drop-offs.
    const stages = buildFunnel(rates({ totalApplications: 50, respondedCount: 0 }));

    expect(findBottleneck(stages)?.key).toBe("responded");
  });

  it("returns null when there is no funnel at all", () => {
    expect(findBottleneck(buildFunnel(rates()))).toBeNull();
  });
});
