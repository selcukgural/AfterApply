import { describe, expect, it } from "vitest";
import { canVote, initialVoteState, voteReducer, type VoteEvent, type VoteState } from "./vote";

const run = (start: VoteState, ...events: VoteEvent[]) => events.reduce(voteReducer, start);

describe("voteReducer", () => {
  it("drops a click while the vote is unknown — the 1 → 2 → 0 bug is a click on a guess", () => {
    const unknown = initialVoteState(null, 1);
    expect(canVote(unknown)).toBe(false);
    expect(voteReducer(unknown, { type: "click" })).toBe(unknown);
  });

  it("removes a known vote on click and keeps what the server confirms", () => {
    const liked = run(initialVoteState(null, 1), { type: "known", on: true, count: 1 });
    expect(canVote(liked)).toBe(true);

    const clicked = voteReducer(liked, { type: "click" });
    expect(clicked).toMatchObject({ on: false, count: 0, busy: true, before: { on: true, count: 1 } });
    expect(canVote(clicked)).toBe(false);

    const settled = voteReducer(clicked, { type: "settled", on: false, count: 0 });
    expect(settled).toEqual({ on: false, count: 0, busy: false, failed: false, before: null });
  });

  it("adds a vote optimistically and takes the server's count over its own guess", () => {
    const state = run(initialVoteState(false, 3), { type: "click" }, { type: "settled", on: true, count: 5 });
    expect(state).toMatchObject({ on: true, count: 5, busy: false });
  });

  it("falls back to the values before the failed toggle, not to the opening ones", () => {
    const opening = initialVoteState(false, 0);
    const afterFirst = run(opening, { type: "click" }, { type: "settled", on: true, count: 1 });
    const failed = run(afterFirst, { type: "click" }, { type: "failed" });
    expect(failed).toEqual({ on: true, count: 1, busy: false, failed: true, before: null });
  });

  it("drops a second click while the first is in flight", () => {
    const inFlight = run(initialVoteState(false, 0), { type: "click" });
    expect(voteReducer(inFlight, { type: "click" })).toBe(inFlight);
  });

  it("clears the failure flag on the next click", () => {
    const failed = run(initialVoteState(false, 0), { type: "click" }, { type: "failed" });
    expect(failed.failed).toBe(true);
    expect(voteReducer(failed, { type: "click" }).failed).toBe(false);
  });

  it("takes a fresh answer from outside unless a toggle is in flight", () => {
    const idle = initialVoteState(false, 2);
    expect(voteReducer(idle, { type: "known", on: true, count: 3 })).toMatchObject({ on: true, count: 3 });

    const inFlight = voteReducer(idle, { type: "click" });
    expect(voteReducer(inFlight, { type: "known", on: false, count: 2 })).toBe(inFlight);
  });

  it("never shows a negative count, even from a stale zero", () => {
    expect(run(initialVoteState(true, 0), { type: "click" }).count).toBe(0);
  });
});
