import { describe, expect, it } from "vitest";
import {
  AUTOSAVE_IDLE_MS,
  AUTOSAVE_MAX_DIRTY_MS,
  autosaveReducer,
  hasUnsavedChanges,
  initialAutosaveState,
  shouldAutosave,
  type AutosaveState,
} from "./autosaveScheduler";

function edited(state: AutosaveState, at: number) {
  return autosaveReducer(state, { type: "edited", at });
}

describe("autosave scheduler", () => {
  it("does nothing while clean", () => {
    const state = initialAutosaveState(1);
    expect(shouldAutosave(state, 60_000)).toBe(false);
    expect(hasUnsavedChanges(state)).toBe(false);
  });

  it("saves once the author has paused for the idle window", () => {
    const state = edited(initialAutosaveState(1), 1_000);
    expect(state.status).toBe("dirty");
    expect(shouldAutosave(state, 1_000 + AUTOSAVE_IDLE_MS - 1)).toBe(false);
    expect(shouldAutosave(state, 1_000 + AUTOSAVE_IDLE_MS)).toBe(true);
  });

  it("saves at the latest every max-dirty window while typing never pauses", () => {
    let state = edited(initialAutosaveState(1), 0);
    // Keystrokes every second: the idle rule never fires, the max-dirty rule does.
    for (let t = 1_000; t < AUTOSAVE_MAX_DIRTY_MS; t += 1_000) {
      state = edited(state, t);
      expect(shouldAutosave(state, t)).toBe(false);
    }
    expect(shouldAutosave(state, AUTOSAVE_MAX_DIRTY_MS)).toBe(true);
  });

  it("does not overlap saves, and saves again for edits made during a request", () => {
    let state = edited(initialAutosaveState(1), 0);
    state = autosaveReducer(state, { type: "saveStarted" });
    expect(state.status).toBe("saving");
    expect(shouldAutosave(state, 60_000)).toBe(false);

    state = edited(state, 2_000);
    expect(state.editedWhileSaving).toBe(true);
    expect(hasUnsavedChanges(state)).toBe(true);

    state = autosaveReducer(state, { type: "saveSucceeded", revision: 2, at: 3_000 });
    expect(state.status).toBe("dirty");
    expect(state.revision).toBe(2);
    expect(state.lastSavedAt).toBe(3_000);
    expect(shouldAutosave(state, 3_000 + AUTOSAVE_MAX_DIRTY_MS)).toBe(true);
  });

  it("is clean after a save with no edits in between", () => {
    let state = edited(initialAutosaveState(1), 0);
    state = autosaveReducer(state, { type: "saveStarted" });
    state = autosaveReducer(state, { type: "saveSucceeded", revision: 2, at: 6_000 });
    expect(state.status).toBe("saved");
    expect(state.dirtySince).toBeNull();
    expect(hasUnsavedChanges(state)).toBe(false);
    expect(shouldAutosave(state, 600_000)).toBe(false);
  });

  it("stops on a conflict and ignores further edits", () => {
    let state = edited(initialAutosaveState(1), 0);
    state = autosaveReducer(state, { type: "saveStarted" });
    state = autosaveReducer(state, { type: "saveConflicted" });
    expect(state.status).toBe("conflict");
    state = edited(state, 10_000);
    expect(state.status).toBe("conflict");
    expect(shouldAutosave(state, 600_000)).toBe(false);
  });

  it("retries a failed save after the idle window, and still counts as unsaved", () => {
    let state = edited(initialAutosaveState(1), 0);
    state = autosaveReducer(state, { type: "saveStarted" });
    state = autosaveReducer(state, { type: "saveFailed", message: "boom", at: 5_000 });
    expect(state.status).toBe("error");
    expect(state.error).toBe("boom");
    expect(hasUnsavedChanges(state)).toBe(true);
    expect(shouldAutosave(state, 5_000 + AUTOSAVE_IDLE_MS - 1)).toBe(false);
    expect(shouldAutosave(state, 5_000 + AUTOSAVE_IDLE_MS)).toBe(true);
  });
});
