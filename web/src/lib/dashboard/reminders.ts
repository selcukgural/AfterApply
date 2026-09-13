import type { ReminderResponse, ReminderType } from "@/types/api";

/** Message key under `dashboard.reminders` for each reminder type. */
export const REMINDER_LABEL_KEY: Record<ReminderType, "followUp" | "possiblyGhosted"> = {
  FollowUp: "followUp",
  PossiblyGhosted: "possiblyGhosted",
};

/**
 * The answer each reminder type asks for, as a message key under `dashboard.reminders`. A reminder
 * is a question — "time to follow up?", "was this ghosted?" — and until 2026-09-13 the only button
 * was "dismiss", which is not an answer to either. "Followed up" records the event; "mark ghosted"
 * changes the status. Both close the reminder.
 */
export const REMINDER_ANSWER_KEY: Record<ReminderType, "followedUp" | "markGhosted"> = {
  FollowUp: "followedUp",
  PossiblyGhosted: "markGhosted",
};

/**
 * The order the panel shows reminders in: the application that has waited longest first, and
 * among equals the reminder created first. The API returns them newest-first, which would put the
 * freshest nudge on top and the one that actually needs attention at the bottom.
 */
export function sortReminders(reminders: readonly ReminderResponse[]): ReminderResponse[] {
  return [...reminders].sort(
    (a, b) => b.daysElapsed - a.daysElapsed || a.createdAt.localeCompare(b.createdAt),
  );
}
