import type { ApplicationStatus } from "@/types/api";
import { appliedAtInputValue } from "@/lib/applications/appliedDate";
import { daysUntil, todayDateOnly } from "@/lib/applications/replyPromise";

/**
 * The "Hatırlatayım" box on an application's page (canvas "İnce dokunuşlar — Paket 2", 2B): what
 * the user may have forgotten and the app did not. Each line appears only when its data does, and
 * the box only when one line does. Pure, so each rule is testable without a browser.
 */

const ENDED: readonly ApplicationStatus[] = ["Accepted", "Rejected", "Withdrawn", "Ghosted"];

export interface ReminderBoxInput {
  id: string;
  status: ApplicationStatus;
  appliedAt: string;
  userMedianResponseDays?: number | null;
  jobPublishedAt?: string | null;
  jobClosedAt?: string | null;
}

export interface EarlierApplication {
  id: string;
  jobTitle: string;
  appliedAt: string;
  status: ApplicationStatus;
}

export interface ReminderBox {
  /** Still waiting for a first answer: the day it is, against the user's own usual wait. */
  patience: { day: number; median: number; early: boolean; progress: number } | null;
  /** How old the posting already was on the day the user applied. */
  postingAgeDays: number | null;
  /** The posting stopped taking applications; `noReply` when nobody has answered yet. */
  closed: { daysAgo: number; noReply: boolean } | null;
  /** The most recent earlier application at the same company. */
  earlier: EarlierApplication | null;
}

/** Whole calendar days from `fromIso` to `toIso`, in the reader's calendar. */
function calendarDays(fromIso: string, toIso: string): number {
  return daysUntil(appliedAtInputValue(toIso), appliedAtInputValue(fromIso));
}

export function buildReminderBox(
  application: ReminderBoxInput,
  sameCompany: readonly EarlierApplication[],
  now: Date = new Date(),
): ReminderBox | null {
  const ended = ENDED.includes(application.status);
  const waiting = application.status === "Applied";
  const today = todayDateOnly(now);

  const closed =
    application.jobClosedAt && !ended
      ? {
          daysAgo: Math.max(0, daysUntil(today, appliedAtInputValue(application.jobClosedAt))),
          noReply: waiting,
        }
      : null;

  const median = application.userMedianResponseDays ?? null;
  // A closed posting says more than the usual wait does, so the patience line steps aside.
  const patience =
    waiting && median !== null && median > 0 && !closed
      ? (() => {
          const day = Math.max(0, daysUntil(today, appliedAtInputValue(application.appliedAt)));
          return { day, median, early: day < median, progress: Math.min(1, day / median) };
        })()
      : null;

  const age = application.jobPublishedAt ? calendarDays(application.jobPublishedAt, application.appliedAt) : null;
  const postingAgeDays = age !== null && age >= 1 ? age : null;

  const appliedAtMs = new Date(application.appliedAt).getTime();
  const earlier =
    sameCompany
      .filter((other) => other.id !== application.id && new Date(other.appliedAt).getTime() < appliedAtMs)
      .sort((a, b) => new Date(b.appliedAt).getTime() - new Date(a.appliedAt).getTime())[0] ?? null;

  if (!patience && postingAgeDays === null && !closed && !earlier) return null;
  return { patience, postingAgeDays, closed, earlier };
}
