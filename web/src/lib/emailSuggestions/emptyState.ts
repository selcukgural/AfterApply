import type { GmailScanStatusResponse } from "@/types/api";

/**
 * Which empty state the Suggestions and Notifications pages show when their list is empty.
 *
 * The "turn on Gmail Scanning" nudge is only right for a user the system has never heard from —
 * for one whose extension has already delivered a signal it reads as "you haven't set this up",
 * which is both wrong and irritating (reported 2026-09-14). The server cannot see the extension's
 * toggle itself, only whether a signal ever arrived, so:
 *
 * - `hasReceivedSignal: true` → "waiting for the next hiring email", no CTA;
 * - `false`, or the status not known (still loading, or the request failed) → the nudge, which
 *   is the pre-existing behaviour and the safe fallback: better to over-nudge on an error than
 *   to hide the one thing a new user needs to do.
 */
export type GmailEmptyStateKind = "nudgeToEnable" | "waitingForEmail";

export function resolveGmailEmptyState(status: GmailScanStatusResponse | undefined): GmailEmptyStateKind {
  return status?.hasReceivedSignal ? "waitingForEmail" : "nudgeToEnable";
}
