import { describe, expect, it } from "vitest";
import {
  FAST_INTERVAL_MS,
  FAST_PHASE_MS,
  GIVE_UP_MS,
  SLOW_INTERVAL_MS,
  formatElapsed,
  isWaitingLong,
  nextPollDelay,
  pendingPhase,
  shortOrderId,
} from "./pollSchedule";

describe("nextPollDelay", () => {
  it("polls fast for the first phase, slowly after, and stops at the limit", () => {
    expect(nextPollDelay(0)).toBe(FAST_INTERVAL_MS);
    expect(nextPollDelay(FAST_PHASE_MS - 1)).toBe(FAST_INTERVAL_MS);
    expect(nextPollDelay(FAST_PHASE_MS)).toBe(SLOW_INTERVAL_MS);
    expect(nextPollDelay(GIVE_UP_MS - 1)).toBe(SLOW_INTERVAL_MS);
    expect(nextPollDelay(GIVE_UP_MS)).toBeNull();
  });

  it("flags the long wait exactly when the slow phase begins", () => {
    expect(isWaitingLong(FAST_PHASE_MS - 1)).toBe(false);
    expect(isWaitingLong(FAST_PHASE_MS)).toBe(true);
  });
});

describe("pendingPhase", () => {
  // The waiting screen's copy and badge follow these three phases; the boundaries are the
  // poll schedule's own, so the text and the cadence can never disagree.
  it("names the phase at the same boundaries the poll uses", () => {
    expect(pendingPhase(0)).toBe("fast");
    expect(pendingPhase(FAST_PHASE_MS - 1)).toBe("fast");
    expect(pendingPhase(FAST_PHASE_MS)).toBe("slow");
    expect(pendingPhase(GIVE_UP_MS - 1)).toBe("slow");
    expect(pendingPhase(GIVE_UP_MS)).toBe("gaveUp");
  });
});

describe("formatElapsed", () => {
  it("renders m:ss, never negative, seconds zero-padded", () => {
    expect(formatElapsed(0)).toBe("0:00");
    expect(formatElapsed(999)).toBe("0:00");
    expect(formatElapsed(42_000)).toBe("0:42");
    expect(formatElapsed(195_400)).toBe("3:15");
    expect(formatElapsed(GIVE_UP_MS)).toBe("10:00");
    expect(formatElapsed(-5_000)).toBe("0:00");
  });
});

describe("shortOrderId", () => {
  it("keeps the head and tail of a GUID and leaves short ids alone", () => {
    expect(shortOrderId("a6bfb552-7481-4db4-9401-a052d5346ed6")).toBe("a6bfb552…46ed6");
    expect(shortOrderId("short")).toBe("short");
  });
});
