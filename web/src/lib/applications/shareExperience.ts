import type { ApplicationStatus } from "@/types/api";

/**
 * The statuses after which the application page offers the candidate experience form
 * (T6). Rejected and ghosted only: the two endings that leave something worth telling the next
 * candidate and nothing else to do about it. A withdrawal was the candidate's own call, and an
 * accepted offer gets its own moment (T7, not built yet).
 */
export function invitesExperience(status: ApplicationStatus): boolean {
  return status === "Rejected" || status === "Ghosted";
}
