import { describe, expect, it } from "vitest";
import { STATUS_BAR_COLORS, STATUS_COLORS } from "./StatusBadge";

describe("status colours keep red for errors, not outcomes (T1, 2026-09-18)", () => {
  it("paints no status red, in the badge or in the bar", () => {
    // The same rule as the dashboard's STATUS_TONE: a rejection is a result, and the list, the
    // timeline and the company view must not become a tally of verdicts.
    for (const palette of [STATUS_COLORS, STATUS_BAR_COLORS]) {
      for (const [status, classes] of Object.entries(palette)) {
        expect(classes, status).not.toMatch(/\bred-|\brose-/);
      }
    }
  });

  it("keeps the two neutral endings distinguishable", () => {
    expect(STATUS_COLORS.Rejected).not.toBe(STATUS_COLORS.Withdrawn);
    expect(STATUS_BAR_COLORS.Rejected).not.toBe(STATUS_BAR_COLORS.Withdrawn);
  });
});
