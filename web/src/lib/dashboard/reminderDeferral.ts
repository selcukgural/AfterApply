import type { ReminderDeferral } from "@/types/api";
import { appliedAtInputValue } from "@/lib/applications/appliedDate";
import { todayDateOnly } from "@/lib/applications/replyPromise";

/**
 * "Moved to today because of the weekend / the feast": said on the morning the held-back reminder
 * appears, and not on the days after — by then it is just a reminder like the others.
 */
export function deferralNote(
  reminder: { deferredFor?: ReminderDeferral | null; deferredUntil?: string | null },
  now: Date = new Date(),
): ReminderDeferral | null {
  if (!reminder.deferredFor || !reminder.deferredUntil) return null;
  return appliedAtInputValue(reminder.deferredUntil) === todayDateOnly(now) ? reminder.deferredFor : null;
}
