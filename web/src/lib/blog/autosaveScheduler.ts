/**
 * The editor's autosave, as a pure state machine so the timing rules can be tested without a
 * browser: a save goes out once the author has paused for `AUTOSAVE_IDLE_MS`, and at the latest
 * every `AUTOSAVE_MAX_DIRTY_MS` while they keep typing — so "every 5 seconds" holds whether the
 * typing stops or not. One save in flight at a time; a save that comes back stale (409) stops the
 * machine until the page is reloaded, because whatever it would save next is built on a copy
 * another tab has already overwritten.
 */
export const AUTOSAVE_IDLE_MS = 5_000;
export const AUTOSAVE_MAX_DIRTY_MS = 5_000;

export type AutosaveStatus = "idle" | "dirty" | "saving" | "saved" | "conflict" | "error";

export interface AutosaveState {
  status: AutosaveStatus;
  /** The revision the server last confirmed — what the next save sends. */
  revision: number;
  /** When the current dirty run started (first edit since the last save), or null when clean. */
  dirtySince: number | null;
  /** When the author last touched anything. */
  lastEditAt: number | null;
  lastSavedAt: number | null;
  /** Whether an edit arrived while a save was in flight — it needs a save of its own. */
  editedWhileSaving: boolean;
  /** The last failure's message, kept for the indicator until the next success. */
  error: string | null;
}

export type AutosaveAction =
  | { type: "edited"; at: number }
  | { type: "saveStarted" }
  | { type: "saveSucceeded"; revision: number; at: number }
  /** The save found nothing worth sending — a new post with nothing written yet — and sent
   *  nothing. Back to clean: there is nothing to lose by leaving. */
  | { type: "saveSkipped" }
  | { type: "saveConflicted" }
  | { type: "saveFailed"; message: string; at: number };

export function initialAutosaveState(revision: number): AutosaveState {
  return {
    status: "idle",
    revision,
    dirtySince: null,
    lastEditAt: null,
    lastSavedAt: null,
    editedWhileSaving: false,
    error: null,
  };
}

export function autosaveReducer(state: AutosaveState, action: AutosaveAction): AutosaveState {
  switch (action.type) {
    case "edited":
      // A stale copy stays stale however much is typed on it.
      if (state.status === "conflict") return state;
      if (state.status === "saving") {
        return { ...state, lastEditAt: action.at, editedWhileSaving: true };
      }
      return {
        ...state,
        status: "dirty",
        lastEditAt: action.at,
        dirtySince: state.dirtySince ?? action.at,
      };
    case "saveStarted":
      return { ...state, status: "saving", editedWhileSaving: false };
    case "saveSucceeded": {
      // Edits that arrived during the request are still unsaved: the next tick picks them up.
      const stillDirty = state.editedWhileSaving;
      return {
        ...state,
        status: stillDirty ? "dirty" : "saved",
        revision: action.revision,
        lastSavedAt: action.at,
        dirtySince: stillDirty ? action.at : null,
        editedWhileSaving: false,
        error: null,
      };
    }
    case "saveSkipped":
      return { ...state, status: "idle", dirtySince: null, editedWhileSaving: false, error: null };
    case "saveConflicted":
      return { ...state, status: "conflict", editedWhileSaving: false };
    case "saveFailed":
      // Still dirty — the content is unsaved — and retried on the next tick after the usual wait.
      return {
        ...state,
        status: "error",
        error: action.message,
        dirtySince: action.at,
        editedWhileSaving: false,
      };
    default:
      return state;
  }
}

/** Whether the tick at `now` should send a save. */
export function shouldAutosave(state: AutosaveState, now: number): boolean {
  if (state.status !== "dirty" && state.status !== "error") return false;
  if (state.dirtySince === null || state.lastEditAt === null) return false;
  // A failed save waits the full idle window before trying again, so a down API is not hammered.
  if (state.status === "error") return now - state.dirtySince >= AUTOSAVE_IDLE_MS;
  return now - state.lastEditAt >= AUTOSAVE_IDLE_MS || now - state.dirtySince >= AUTOSAVE_MAX_DIRTY_MS;
}

/** Whether leaving the page now would lose something. */
export function hasUnsavedChanges(state: AutosaveState): boolean {
  return state.status === "dirty" || state.status === "saving" || state.status === "error" || state.editedWhileSaving;
}
