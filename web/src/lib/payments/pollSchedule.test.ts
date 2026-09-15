import { describe, expect, it } from "vitest";
import { FAST_INTERVAL_MS, FAST_PHASE_MS, GIVE_UP_MS, SLOW_INTERVAL_MS, isWaitingLong, nextPollDelay } from "./pollSchedule";

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
