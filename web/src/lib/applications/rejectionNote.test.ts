import { describe, expect, it } from "vitest";
import { latestRejectionReason, REJECTION_TIPS } from "./rejectionNote";

describe("latestRejectionReason", () => {
  it("reads the reason on the latest move to Rejected", () => {
    expect(
      latestRejectionReason([
        { toStatus: "Rejected", changedAt: "2026-03-01T10:00:00Z", rejectionReasonCategory: "LocationOrRelocation" },
        { toStatus: "Screening", changedAt: "2026-03-05T10:00:00Z", rejectionReasonCategory: null },
        { toStatus: "Rejected", changedAt: "2026-03-10T10:00:00Z", rejectionReasonCategory: "ExperienceLevelMismatch" },
      ]),
    ).toBe("ExperienceLevelMismatch");
  });

  it("is null when the company stated none", () => {
    expect(latestRejectionReason([{ toStatus: "Rejected", changedAt: "2026-03-10T10:00:00Z", rejectionReasonCategory: "NotStated" }])).toBeNull();
    expect(latestRejectionReason([{ toStatus: "Rejected", changedAt: "2026-03-10T10:00:00Z", rejectionReasonCategory: null }])).toBeNull();
    expect(latestRejectionReason(undefined)).toBeNull();
  });
});

describe("REJECTION_TIPS", () => {
  it("offers a tip only for reasons a person can act on", () => {
    expect(REJECTION_TIPS.ExperienceLevelMismatch).toBe("experience");
    expect(REJECTION_TIPS.PositionCancelledOrFilled).toBeUndefined();
    expect(REJECTION_TIPS.Other).toBeUndefined();
  });
});
