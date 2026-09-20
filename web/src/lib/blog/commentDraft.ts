/** The comment rules as the API enforces them (BlogComment.MinContentLength / MaxContentLength). */
export const COMMENT_MIN_LENGTH = 10;
export const COMMENT_MAX_LENGTH = 3000;

export type CommentDraftProblem = "empty" | "tooShort" | "tooLong";

/**
 * The form's own check before a request goes out — the same rule as the API's validator, so a
 * reader learns about a too-short comment without a round trip. Never a substitute for it: the
 * API validates again.
 */
export function validateCommentDraft(content: string): CommentDraftProblem | null {
  const trimmed = content.trim();
  if (trimmed.length === 0) return "empty";
  if (trimmed.length < COMMENT_MIN_LENGTH) return "tooShort";
  if (trimmed.length > COMMENT_MAX_LENGTH) return "tooLong";
  return null;
}

/**
 * "2 saat önce", "dün", "3 gün önce" — the platform's own wording for the locale
 * (`Intl.RelativeTimeFormat`), and past a week the plain date, since "34 gün önce" says less
 * than "17 Ağu 2026". `now` is a parameter so a server render and its hydration agree.
 */
export function relativeTime(iso: string, locale: string, now: Date): string {
  const then = new Date(iso);
  const seconds = Math.round((now.getTime() - then.getTime()) / 1000);
  const rtf = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  if (seconds < 60) return rtf.format(0, "second");
  const minutes = Math.round(seconds / 60);
  if (minutes < 60) return rtf.format(-minutes, "minute");
  const hours = Math.round(minutes / 60);
  if (hours < 24) return rtf.format(-hours, "hour");
  const days = Math.round(hours / 24);
  if (days < 7) return rtf.format(-days, "day");
  return new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(then);
}
