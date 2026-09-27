import { calendarDaysUntil } from "@/lib/applications/interview";

/**
 * The interview-morning card (canvas "İnce dokunuşlar — Paket 2", 1B): shown for an interview that
 * is today and has not been over for long. Past this grace the reminders card takes over with its
 * "how did it go?" question.
 */
export const MORNING_GRACE_MS = 2 * 60 * 60 * 1000;

export function isInterviewMorning(interviewAtIso: string, now: Date = new Date()): boolean {
  const at = new Date(interviewAtIso).getTime();
  return calendarDaysUntil(interviewAtIso, now) === 0 && now.getTime() < at + MORNING_GRACE_MS;
}

export type Countdown = { kind: "later"; hours: number; minutes: number } | { kind: "started" };

/** "3 h 20 min left", rounded up to the minute so it never reads 0 min before the start. */
export function countdown(interviewAtIso: string, now: Date = new Date()): Countdown {
  const ms = new Date(interviewAtIso).getTime() - now.getTime();
  if (ms <= 0) return { kind: "started" };
  const totalMinutes = Math.ceil(ms / 60_000);
  return { kind: "later", hours: Math.floor(totalMinutes / 60), minutes: totalMinutes % 60 };
}

/** The first `max` characters of a posting's text, cut at a word, with an ellipsis when cut. */
export function excerpt(text: string, max = 260): string {
  const clean = text.replace(/\s+/g, " ").trim();
  if (clean.length <= max) return clean;
  const cut = clean.slice(0, max);
  const lastSpace = cut.lastIndexOf(" ");
  return `${(lastSpace > max * 0.6 ? cut.slice(0, lastSpace) : cut).trimEnd()}…`;
}

/**
 * Puts a space after every block boundary of a posting's stored HTML, so its text reads as words
 * once the tags are gone — `textContent` alone runs "…queues</li><li>Observability" together. Only
 * adds spaces; what is left is still parsed (never executed) and read back as text.
 */
export function spaceBlocks(html: string): string {
  return html.replace(/<\/(p|li|h[1-6]|div|ul|ol)>|<br\s*\/?>/gi, "$& ");
}
