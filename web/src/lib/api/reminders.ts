import type { ReminderResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * Follow-up and "possibly ghosted" reminders. The API has generated these from a daily scan since
 * the first release; until 2026-09-13 nothing on the web read them, which is why the landing page
 * promised reminders nobody could see.
 */
export const remindersApi = {
  list: () => apiFetch<ReminderResponse[]>("/api/reminders"),

  dismiss: (id: string) => apiFetch<void>(`/api/reminders/${id}/dismiss`, { method: "POST" }),
};
