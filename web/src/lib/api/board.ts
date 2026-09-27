import type {
  BoardColumn,
  BoardColumnPage,
  BoardFilter,
  BoardResponse,
  MoveBoardCardRequest,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** Cards per column on each read — the board opens with this many and loads this many more per
 *  scroll. */
export const BOARD_PAGE_SIZE = 10;

export function boardQueryString(filter: BoardFilter, extra: Record<string, string | undefined> = {}): string {
  const params = new URLSearchParams();
  if (filter.search) params.set("search", filter.search);
  for (const source of filter.sources ?? []) params.append("sources", source);
  if (filter.activeFrom) params.set("activeFrom", filter.activeFrom);
  if (filter.activeTo) params.set("activeTo", filter.activeTo);
  if (filter.silentOnly) params.set("silentOnly", "true");
  if (filter.withReminder) params.set("withReminder", "true");
  if (filter.withPromise) params.set("withPromise", "true");
  if (filter.unseenOnly) params.set("unseenOnly", "true");
  for (const [key, value] of Object.entries(extra)) {
    if (value !== undefined) params.set(key, value);
  }
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export const boardApi = {
  get: (filter: BoardFilter) =>
    apiFetch<BoardResponse>(`/api/board${boardQueryString(filter, { limit: String(BOARD_PAGE_SIZE) })}`),

  getColumn: (column: BoardColumn, filter: BoardFilter, cursor: string) =>
    apiFetch<BoardColumnPage>(
      `/api/board/columns/${column}${boardQueryString(filter, { limit: String(BOARD_PAGE_SIZE), cursor })}`,
    ),

  add: (applicationIds: string[], trackedJobIds: string[] = []) =>
    apiFetch<{ added: number }>("/api/board/cards", {
      method: "POST",
      body: JSON.stringify({ applicationIds, trackedJobIds }),
    }),

  remove: (cardId: string) => apiFetch<void>(`/api/board/cards/${cardId}`, { method: "DELETE" }),

  move: (cardId: string, request: MoveBoardCardRequest) =>
    apiFetch<void>(`/api/board/cards/${cardId}/move`, { method: "POST", body: JSON.stringify(request) }),

  markSeen: (request: { cardIds: string[] | null; all: boolean }) =>
    apiFetch<void>("/api/board/cards/seen", { method: "POST", body: JSON.stringify(request) }),
};
