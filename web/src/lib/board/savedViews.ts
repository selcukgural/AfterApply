import { type BoardFilterState, EMPTY_BOARD_FILTER } from "./board";

export interface SavedBoardView {
  name: string;
  filter: BoardFilterState;
}

/** Listed on the cookie policy (storage.item3) and in the storage tripwire test. */
const STORAGE_KEY = "aa_board_views";
export const MAX_SAVED_VIEWS = 12;
export const MAX_VIEW_NAME = 40;

/**
 * Saved board views are a per-browser convenience, not account data: never sent to the server,
 * read and written defensively, and the board behaves the same when storage is unavailable
 * (private mode, storage switched off).
 */
export function readSavedViews(): SavedBoardView[] {
  try {
    const raw = window.localStorage.getItem(STORAGE_KEY);
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    if (!Array.isArray(parsed)) return [];
    return parsed
      .filter(
        (view): view is SavedBoardView =>
          typeof view === "object" && view !== null && typeof view.name === "string" && typeof view.filter === "object",
      )
      .map((view) => ({ name: view.name.slice(0, MAX_VIEW_NAME), filter: { ...EMPTY_BOARD_FILTER, ...view.filter } }))
      .slice(0, MAX_SAVED_VIEWS);
  } catch {
    return [];
  }
}

export function writeSavedViews(views: SavedBoardView[]): void {
  try {
    if (views.length === 0) {
      window.localStorage.removeItem(STORAGE_KEY);
    } else {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify(views.slice(-MAX_SAVED_VIEWS)));
    }
  } catch {
    // Storage unavailable: the view simply is not remembered.
  }
}

/** The list after saving `view`: a view with the same name is replaced, the oldest drops off past the cap. */
export function withView(views: SavedBoardView[], view: SavedBoardView): SavedBoardView[] {
  return [...views.filter((existing) => existing.name !== view.name), view].slice(-MAX_SAVED_VIEWS);
}
