import type { UpcomingInterviewResponse } from "@/types/api";
import { calendarDaysUntil } from "@/lib/applications/interview";

const COUNT_PREFIX = /^\(\d+\+?\) /;

/**
 * The page title with a "(3) " prefix while something is waiting, so it shows on the browser tab
 * while the user is in another one. Idempotent: an already-prefixed title is re-prefixed, never
 * stacked, and a zero count strips the prefix.
 */
export function titleWithCount(title: string, count: number): string {
  const bare = title.replace(COUNT_PREFIX, "");
  if (count <= 0) return bare;
  return `(${count > 99 ? "99+" : count}) ${bare}`;
}

/** What is waiting on the user right now: reminders that are due plus today's interviews. */
export function pendingCount(
  dueReminders: number,
  upcomingInterviews: readonly UpcomingInterviewResponse[],
  now: Date = new Date(),
): number {
  const today = upcomingInterviews.filter((interview) => calendarDaysUntil(interview.interviewAt, now) === 0).length;
  return Math.max(0, dueReminders) + today;
}
