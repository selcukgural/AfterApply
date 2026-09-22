import type { ApplicationStatus, RejectionNotice, ReplyPromiseOutcome } from "@/types/api";

/**
 * The client half of the two questions the status panel and the application page ask: "did they
 * give you a date?" and "how did you learn of the rejection?". Whether a promise is kept is decided
 * on the server (ReplyPromises) and arrives as `promisedReplyOutcome`; this file only formats and
 * gates, so the page never judges a promise differently from the reminders and the figures.
 */

/** Mirrors TerminalApplicationStatuses on the server: nothing is left to wait for once closed. */
const CLOSED_STATUSES: ReadonlySet<ApplicationStatus> = new Set<ApplicationStatus>(["Accepted", "Rejected", "Withdrawn", "Ghosted"]);

export function isClosedStatus(status: ApplicationStatus): boolean {
  return CLOSED_STATUSES.has(status);
}

/** The panel asks for a date only when moving into a status that is still in play. */
export function asksForPromise(newStatus: ApplicationStatus): boolean {
  return !isClosedStatus(newStatus);
}

/** The panel asks how the rejection was learned of only when the new status is Rejected. */
export function asksForRejectionNotice(newStatus: ApplicationStatus): boolean {
  return newStatus === "Rejected";
}

/** Order the options are offered in — the one that counts in the company's favour first. */
export const REJECTION_NOTICE_OPTIONS: readonly RejectionNotice[] = ["CompanyNotified", "SeenOnPortal", "OtherOrInferred"];

/** Mirrors ReplyPromiseRules.MaxDaysFromToday: the server refuses dates further away than this. */
export const MAX_PROMISE_DAYS_FROM_TODAY = 365;

function parseDateOnly(value: string): { y: number; m: number; d: number } | null {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return null;
  return { y: Number(match[1]), m: Number(match[2]), d: Number(match[3]) };
}

/**
 * A YYYY-MM-DD calendar date in the reader's format. Built from its parts in local time: `new
 * Date("2026-10-10")` is UTC midnight, which west of Greenwich prints as the 9th.
 */
export function formatPromiseDate(value: string, locale: string): string {
  const parts = parseDateOnly(value);
  if (!parts) return value;
  return new Date(parts.y, parts.m - 1, parts.d).toLocaleDateString(locale);
}

/** Today as YYYY-MM-DD in the reader's own calendar — what a date input's value looks like. */
export function todayDateOnly(now: Date = new Date()): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}

/** Whole calendar days from `today` to `value`: positive ahead, negative past, 0 on the day. */
export function daysUntil(value: string, today: string): number {
  const a = parseDateOnly(value);
  const b = parseDateOnly(today);
  if (!a || !b) return 0;
  const ms = Date.UTC(a.y, a.m - 1, a.d) - Date.UTC(b.y, b.m - 1, b.d);
  return Math.round(ms / 86_400_000);
}

/** The range a date input offers, so a typo cannot silence the follow-up reminder for years. */
export function promiseDateBounds(today: string): { min: string; max: string } {
  const b = parseDateOnly(today);
  if (!b) return { min: today, max: today };
  const shift = (days: number) => {
    const d = new Date(Date.UTC(b.y, b.m - 1, b.d + days));
    return d.toISOString().slice(0, 10);
  };
  return { min: shift(-MAX_PROMISE_DAYS_FROM_TODAY), max: shift(MAX_PROMISE_DAYS_FROM_TODAY) };
}

export type PromiseLineTone = "neutral" | "good" | "warn";

/**
 * What the application page says beside a promise, as a message key under
 * `applications.detail.promise` plus its count and tone. The outcome is the server's; the day
 * count is the reader's own calendar, so "4 days left" is counted from the day they are looking.
 * Null for a voided promise — the candidate withdrew before the date, and there is nothing to say.
 */
export function promiseLine(
  outcome: ReplyPromiseOutcome,
  promisedReplyBy: string,
  today: string,
): { key: "daysLeft" | "dueToday" | "overdue" | "kept" | "late"; count: number; tone: PromiseLineTone } | null {
  const days = daysUntil(promisedReplyBy, today);
  switch (outcome) {
    case "Pending":
      return days <= 0 ? { key: "dueToday", count: 0, tone: "neutral" } : { key: "daysLeft", count: days, tone: "neutral" };
    case "Overdue":
      // At least one: the server calls it overdue from the day after, whatever the reader's zone.
      return { key: "overdue", count: Math.max(1, -days), tone: "warn" };
    case "Kept":
      return { key: "kept", count: 0, tone: "good" };
    case "Late":
      return { key: "late", count: 0, tone: "warn" };
    case "Void":
      return null;
  }
}
