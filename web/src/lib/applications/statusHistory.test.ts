import { describe, expect, it } from "vitest";
import { hasEmailContext, hasStatedRejectionReason } from "./statusHistory";

describe("hasStatedRejectionReason", () => {
  it("treats NotStated as no reason — it is the majority outcome, not information", () => {
    expect(hasStatedRejectionReason({ rejectionReasonCategory: "NotStated" })).toBe(false);
  });

  it("is false when the extraction step never ran", () => {
    expect(hasStatedRejectionReason({ rejectionReasonCategory: null })).toBe(false);
  });

  it("is true for a real stated reason", () => {
    expect(hasStatedRejectionReason({ rejectionReasonCategory: "LocationOrRelocation" })).toBe(true);
  });
});

describe("hasEmailContext", () => {
  it("is false once the originating suggestion is gone", () => {
    expect(hasEmailContext({ emailSubject: null, emailSnippet: null })).toBe(false);
  });

  it("is true when either half survived", () => {
    expect(hasEmailContext({ emailSubject: "Update on your application", emailSnippet: null })).toBe(true);
    expect(hasEmailContext({ emailSubject: null, emailSnippet: "we have decided..." })).toBe(true);
  });
});
