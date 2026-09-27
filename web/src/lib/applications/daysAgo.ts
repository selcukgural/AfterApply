import { appliedAtInputValue } from "@/lib/applications/appliedDate";
import { daysUntil, todayDateOnly } from "@/lib/applications/replyPromise";

/**
 * "today", "yesterday", "12 days ago", "3 months ago": how long ago a day-granular date was, in
 * the reader's own calendar. Built for the applied date, where "12 days ago" answers the question
 * people actually have ("is it too early to chase?") and the exact date is one hover away.
 */
export function daysAgo(iso: string, locale: string, now: Date = new Date()): string {
  const days = Math.max(0, -daysUntil(appliedAtInputValue(iso), todayDateOnly(now)));
  // "today" and "yesterday" read better than "0/1 days ago"; past that, numbers. Turkish "auto"
  // turns 2 days into "evvelsi gün", which reads archaic and is easy to misread as yesterday.
  if (days <= 1) return new Intl.RelativeTimeFormat(locale, { numeric: "auto" }).format(-days, "day");
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "always" });
  if (days < 30) return rtf.format(-days, "day");
  if (days < 365) return rtf.format(-Math.floor(days / 30), "month");
  return rtf.format(-Math.floor(days / 365), "year");
}
