import { describe, expect, it } from "vitest";
import { deferralNote } from "./reminderDeferral";

describe("deferralNote", () => {
  const monday9 = new Date(2026, 9, 5, 9, 0).toISOString();

  it("names the reason on the morning it was moved to", () => {
    expect(deferralNote({ deferredFor: "Weekend", deferredUntil: monday9 }, new Date(2026, 9, 5, 14, 0))).toBe("Weekend");
  });

  it("says nothing on the following days", () => {
    expect(deferralNote({ deferredFor: "Weekend", deferredUntil: monday9 }, new Date(2026, 9, 6, 9, 0))).toBeNull();
  });

  it("says nothing for a reminder that was not held back", () => {
    expect(deferralNote({ deferredFor: null, deferredUntil: null })).toBeNull();
    expect(deferralNote({})).toBeNull();
  });
});
