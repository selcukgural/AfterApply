import type { ReminderResponse, ReminderType } from "@/types/api";

/** Message key under `dashboard.reminders` for each reminder type. */
export const REMINDER_LABEL_KEY: Record<ReminderType, "followUp" | "possiblyGhosted"> = {
  FollowUp: "followUp",
  PossiblyGhosted: "possiblyGhosted",
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
