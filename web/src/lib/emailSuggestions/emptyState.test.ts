import { describe, expect, it } from "vitest";
import { resolveGmailEmptyState } from "./emptyState";

describe("resolveGmailEmptyState", () => {
  it("stops nudging once the extension has delivered a Gmail signal", () => {
    expect(resolveGmailEmptyState({ hasReceivedSignal: true })).toBe("waitingForEmail");
  });

  it("nudges a user the system has never heard from", () => {
    expect(resolveGmailEmptyState({ hasReceivedSignal: false })).toBe("nudgeToEnable");
  });

  it("falls back to the nudge while the status is unknown, rather than hiding the setup step", () => {
    expect(resolveGmailEmptyState(undefined)).toBe("nudgeToEnable");
  });
});
