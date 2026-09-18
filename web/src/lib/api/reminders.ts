import type {
  BulkChangeStatusResponse,
  BulkReminderRequest,
  BulkReminderResponse,
  PagedResult,
  ReminderPauseResponse,
  ReminderResponse,
  UndoBulkStatusEntry,
  UndoBulkStatusResponse,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** The dashboard card's height, and so the page size the list is asked for. */
export const REMINDER_PAGE_SIZE = 5;

/**
 * Follow-up and "possibly ghosted" reminders. The API has generated these from a daily scan since
 * the first release; until 2026-09-13 nothing on the web read them, which is why the landing page
 * promised reminders nobody could see. Paged since the same day, once a real account showed what
 * "all of them in one response" meant (1,224 rows).
 */
export const remindersApi = {
  list: (page: number, pageSize = REMINDER_PAGE_SIZE) =>
    apiFetch<PagedResult<ReminderResponse>>(`/api/reminders?page=${page}&pageSize=${pageSize}`),

  dismiss: (id: string) => apiFetch<void>(`/api/reminders/${id}/dismiss`, { method: "POST" }),

  /** "I followed up": records the FollowUpSent event on the application and closes the reminder. */
  followUp: (id: string) => apiFetch<void>(`/api/reminders/${id}/follow-up`, { method: "POST" }),

  /** The three answers, for a selection. Each throws ApiError 409 (body: BulkCountMismatchProblem)
   *  when an `all` selection's count has moved since the card was drawn — nothing changed then. */
  bulkDismiss: (request: BulkReminderRequest) =>
    apiFetch<BulkReminderResponse>("/api/reminders/bulk/dismiss", { method: "POST", body: JSON.stringify(request) }),

  bulkFollowUp: (request: BulkReminderRequest) =>
    apiFetch<BulkReminderResponse>("/api/reminders/bulk/follow-up", { method: "POST", body: JSON.stringify(request) }),

  /** Moves the applications behind the selection to Ghosted; the reminders close with them. The
   *  response is a bulk status change's, which is what bulkGhostUndo takes back. */
  bulkGhost: (request: BulkReminderRequest) =>
    apiFetch<BulkChangeStatusResponse>("/api/reminders/bulk/ghost", { method: "POST", body: JSON.stringify(request) }),

  bulkGhostUndo: (entries: UndoBulkStatusEntry[]) =>
    apiFetch<UndoBulkStatusResponse>("/api/reminders/bulk/ghost/undo", {
      method: "POST",
      body: JSON.stringify({ entries }),
    }),

  /** The break (T5): where it stands, start one, end one early, and the two answers to the
   *  question that greets the return. */
  getPause: () => apiFetch<ReminderPauseResponse>("/api/reminders/pause"),

  pause: (days: BreakLength) =>
    apiFetch<ReminderPauseResponse>("/api/reminders/pause", { method: "PUT", body: JSON.stringify({ days }) }),

  endPause: () => apiFetch<ReminderPauseResponse>("/api/reminders/pause/end", { method: "POST" }),

  acknowledgePause: () => apiFetch<void>("/api/reminders/pause/acknowledge", { method: "POST" }),

  /** Ghosts everything that went quiet during the break and clears it; bulkGhostUndo takes it back. */
  closeSilenced: () => apiFetch<BulkChangeStatusResponse>("/api/reminders/pause/close-silenced", { method: "POST" }),
};

/** The three lengths the API accepts (PauseRemindersRequestValidator). */
export const BREAK_LENGTHS = [7, 14, 30] as const;
export type BreakLength = (typeof BREAK_LENGTHS)[number];
