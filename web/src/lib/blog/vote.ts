/**
 * The one toggle a reader has on a post ("like") and on a comment ("helpful"), as a state
 * machine the pill renders (2026-09-21). It exists because the page is server-rendered without
 * the reader's token, so the pill starts out *not knowing* whether this reader already voted —
 * and a click on an unknown state used to add one on screen while the server took one away
 * (1 → 2 → 0). The rule here: no click goes through until the vote is known, and a failed
 * toggle falls back to the values just before it, never to the page's opening ones.
 */
export interface VoteSnapshot {
  on: boolean;
  count: number;
}

export interface VoteState {
  /** Null until the server has said whether this reader voted — the pill is inert until then. */
  on: boolean | null;
  count: number;
  /** A toggle is in flight: the pill is inert and a second click is dropped, not queued. */
  busy: boolean;
  /** The last toggle failed; cleared by the next click. */
  failed: boolean;
  /** Where a failing toggle goes back to. Set on click, cleared once the server has answered. */
  before: VoteSnapshot | null;
}

export type VoteEvent =
  /** The server said (a fresh list, or the pill's own first question). Ignored mid-toggle — the toggle's own answer is fresher. */
  | { type: "known"; on: boolean | null; count: number }
  | { type: "click" }
  | { type: "settled"; on: boolean; count: number }
  | { type: "failed" };

export function initialVoteState(on: boolean | null, count: number): VoteState {
  return { on, count, busy: false, failed: false, before: null };
}

/** True when a click would do something: the vote is known and nothing is in flight. */
export function canVote(state: VoteState): boolean {
  return state.on !== null && !state.busy;
}

export function voteReducer(state: VoteState, event: VoteEvent): VoteState {
  switch (event.type) {
    case "known":
      if (state.busy) return state;
      return { ...state, on: event.on, count: event.count };
    case "click": {
      if (!canVote(state)) return state;
      const on = state.on === true;
      return {
        on: !on,
        count: Math.max(0, state.count + (on ? -1 : 1)),
        busy: true,
        failed: false,
        before: { on, count: state.count },
      };
    }
    case "settled":
      return { on: event.on, count: event.count, busy: false, failed: false, before: null };
    case "failed":
      return {
        on: state.before?.on ?? state.on,
        count: state.before?.count ?? state.count,
        busy: false,
        failed: true,
        before: null,
      };
  }
}
