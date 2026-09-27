import { todayDateOnly } from "@/lib/applications/replyPromise";

/**
 * The value a `<input type="date">` shows for an application's applied date, in the user's own
 * calendar. Slicing an ISO string instead reads the UTC date, which in Türkiye is still
 * "yesterday" between 00:00 and 03:00 — and disagrees with the list, which shows the local date.
 */
export function appliedAtInputValue(appliedAtIso?: string | null, now: Date = new Date()): string {
  return todayDateOnly(appliedAtIso ? new Date(appliedAtIso) : now);
}
