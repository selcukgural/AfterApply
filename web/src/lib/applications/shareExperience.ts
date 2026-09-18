import type { ApplicationStatus } from "@/types/api";

/**
 * The two moments at which an application page offers the candidate experience form, because
 * the process has ended and the person now knows something the next candidate cannot find out
 * anywhere else:
 *
 * - `"ending"` — rejected or ghosted (T6): a quiet line, no box, the offer is the whole point.
 * - `"accepted"` — an accepted offer (T7): the same offer, framed as the last thing to leave
 *   here before the account goes quiet, next to "your data is yours, the account waits".
 *
 * A withdrawal was the candidate's own call and gets neither; while the process is open there is
 * nothing to tell yet.
 */
export type ClosingMoment = "ending" | "accepted";

export function closingMoment(status: ApplicationStatus): ClosingMoment | null {
  if (status === "Rejected" || status === "Ghosted") return "ending";
  if (status === "Accepted") return "accepted";
  return null;
}

/** The statuses after which the page's *ending* line offers the form (T6). */
export function invitesExperience(status: ApplicationStatus): boolean {
  return closingMoment(status) === "ending";
}
