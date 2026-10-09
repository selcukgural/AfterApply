import type { ApplicationStatus, InterviewFormat, ReminderType } from "@/types/api";
import type { SnoozeDays } from "@/lib/api/reminders";

/**
 * The client half of the interview date: which stages ask for one, how its instant is built from
 * the two inputs and read back into them, how the card names the day, and the calendar file and
 * link. Everything here is pure so it can be tested without a browser; the file download itself
 * lives in AddToCalendar.
 */

/** Mirrors InterviewStages on the server: the stages an interview date can be recorded in. */
export const INTERVIEW_STAGES: readonly ApplicationStatus[] = ["Screening", "Interview", "TechnicalInterview", "FinalInterview"];

export function asksForInterview(status: ApplicationStatus): boolean {
  return INTERVIEW_STAGES.includes(status);
}

export const INTERVIEW_FORMATS: readonly InterviewFormat[] = ["Online", "InPerson", "Phone"];

/** Mirrors InterviewOutcomeRequestValidator.NextStatuses, in pipeline order. */
const NEXT_STAGES: readonly ApplicationStatus[] = ["Interview", "TechnicalInterview", "FinalInterview", "Offer", "Accepted"];

/**
 * The stages "I moved on" offers after an interview in `current`: everything after it, so a
 * technical round does not offer "Interview" again. Unknown or earlier stages get the whole list.
 */
export function nextStagesAfter(current: ApplicationStatus | null | undefined): ApplicationStatus[] {
  const index = current ? NEXT_STAGES.indexOf(current) : -1;
  return NEXT_STAGES.slice(index + 1);
}

/** Mirrors Application.InterviewWithMaxLength on the server. */
export const INTERVIEW_WITH_MAX_LENGTH = 200;

/** Mirrors InterviewRules on the server. */
export const MAX_INTERVIEW_DAYS_BACK = 30;
export const MAX_INTERVIEW_DAYS_AHEAD = 365;

const pad = (n: number) => String(n).padStart(2, "0");

/**
 * The instant a date input and a time input describe, in the reader's own zone, as an ISO string.
 * Null when either is empty or malformed. `new Date("2026-09-28T14:00")` is local time by spec,
 * which is exactly what "14:00" typed in Istanbul means.
 */
export function toInterviewInstant(date: string, time: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date) || !/^\d{2}:\d{2}$/.test(time)) return null;
  const at = new Date(`${date}T${time}`);
  return Number.isNaN(at.getTime()) ? null : at.toISOString();
}

/** The two input values for an instant, in the reader's zone — the reverse of toInterviewInstant. */
export function splitInstant(iso: string): { date: string; time: string } {
  const at = new Date(iso);
  return {
    date: `${at.getFullYear()}-${pad(at.getMonth() + 1)}-${pad(at.getDate())}`,
    time: `${pad(at.getHours())}:${pad(at.getMinutes())}`,
  };
}

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

/** Whole calendar days from `now` to `iso` in the reader's zone: 0 today, 1 tomorrow, -1 yesterday. */
export function calendarDaysUntil(iso: string, now: Date = new Date()): number {
  return Math.round((startOfDay(new Date(iso)) - startOfDay(now)) / 86_400_000);
}

export type InterviewDayKey = "today" | "tomorrow" | "yesterday" | "onDate";

/** Which of the card's day words fits the interview: "today", "tomorrow", "yesterday", or a date. */
export function interviewDayKey(iso: string, now: Date = new Date()): InterviewDayKey {
  switch (calendarDaysUntil(iso, now)) {
    case 0:
      return "today";
    case 1:
      return "tomorrow";
    case -1:
      return "yesterday";
    default:
      return "onDate";
  }
}

/** "14:00" in the reader's locale and zone. */
export function formatInterviewTime(iso: string, locale: string): string {
  return new Date(iso).toLocaleTimeString(locale, { hour: "2-digit", minute: "2-digit" });
}

/** "28 Eyl Pzt" / "Mon, Sep 28" in the reader's locale and zone. */
export function formatInterviewDate(iso: string, locale: string): string {
  return new Date(iso).toLocaleDateString(locale, { day: "numeric", month: "short", weekday: "short" });
}

/** The snooze lengths a row's menu offers: an interview question is only worth a day or three,
 *  anything else three days to a fortnight. */
export function snoozeOptionsFor(type: ReminderType): SnoozeDays[] {
  return type === "InterviewHeld" ? [1, 3] : [3, 7, 14];
}

/** The day a snoozed row comes back, for the "back on …" line. */
export function snoozedUntil(days: SnoozeDays, now: Date = new Date()): Date {
  return new Date(now.getTime() + days * 86_400_000);
}

// --- Calendar -------------------------------------------------------------------------------------

/** How long the calendar entry is. Nobody tells a candidate; an hour is what a calendar expects. */
export const INTERVIEW_CALENDAR_MINUTES = 60;

export interface CalendarEvent {
  /** Stable per interview, so importing the same file twice updates rather than duplicates. */
  uid: string;
  start: string;
  title: string;
  description: string;
}

/** 20260928T110000Z — the UTC form both iCalendar and Google's template link take. */
export function toCalendarStamp(iso: string): string {
  return new Date(iso).toISOString().replace(/[-:]/g, "").replace(/\.\d{3}/, "");
}

function endOf(event: CalendarEvent): string {
  return new Date(new Date(event.start).getTime() + INTERVIEW_CALENDAR_MINUTES * 60_000).toISOString();
}

/** RFC 5545 §3.3.11 TEXT escaping: backslash, semicolon, comma and newlines. Company names and job
 *  titles are the user's own text and can hold any of them. */
export function escapeIcsText(value: string): string {
  return value
    .replace(/\\/g, "\\\\")
    .replace(/;/g, "\\;")
    .replace(/,/g, "\\,")
    .replace(/\r?\n|\r/g, "\\n");
}

/** RFC 5545 §3.1 line folding: lines longer than 75 octets continue on the next line after a space.
 *  Counted in UTF-8 bytes and never split inside a character, since titles are often Turkish. */
export function foldIcsLine(line: string): string {
  const encoder = new TextEncoder();
  const parts: string[] = [];
  let current = "";
  let bytes = 0;
  for (const char of line) {
    const size = encoder.encode(char).length;
    const limit = parts.length === 0 ? 75 : 74;
    if (bytes + size > limit) {
      parts.push(current);
      current = "";
      bytes = 0;
    }
    current += char;
    bytes += size;
  }
  parts.push(current);
  return parts.join("\r\n ");
}

/** A one-event iCalendar file with a reminder an hour before. Opens in Outlook, Apple Calendar and
 *  Google Calendar's import alike. */
export function buildIcs(event: CalendarEvent, now: Date = new Date()): string {
  const lines = [
    "BEGIN:VCALENDAR",
    "VERSION:2.0",
    "PRODID:-//e-kariyerim//Interview//EN",
    "CALSCALE:GREGORIAN",
    "METHOD:PUBLISH",
    "BEGIN:VEVENT",
    `UID:${event.uid}@ekariyerim.com`,
    `DTSTAMP:${toCalendarStamp(now.toISOString())}`,
    `DTSTART:${toCalendarStamp(event.start)}`,
    `DTEND:${toCalendarStamp(endOf(event))}`,
    `SUMMARY:${escapeIcsText(event.title)}`,
    `DESCRIPTION:${escapeIcsText(event.description)}`,
    "BEGIN:VALARM",
    "ACTION:DISPLAY",
    `DESCRIPTION:${escapeIcsText(event.title)}`,
    "TRIGGER:-PT1H",
    "END:VALARM",
    "END:VEVENT",
    "END:VCALENDAR",
  ];
  return lines.map(foldIcsLine).join("\r\n") + "\r\n";
}

/** Google Calendar's "add event" template link. Opens a pre-filled form the user saves themselves —
 *  nothing is written to their calendar without them, and no Google permission is involved. */
export function googleCalendarUrl(event: CalendarEvent): string {
  const params = new URLSearchParams({
    action: "TEMPLATE",
    text: event.title,
    dates: `${toCalendarStamp(event.start)}/${toCalendarStamp(endOf(event))}`,
    details: event.description,
  });
  return `https://calendar.google.com/calendar/render?${params.toString()}`;
}
