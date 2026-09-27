import type { ApplicationStatusHistoryResponse, RejectionReasonCategory } from "@/types/api";
import { hasStatedRejectionReason } from "@/lib/applications/statusHistory";

/**
 * The calm note on a rejected application (canvas "İnce dokunuşlar — Paket 2", 4A): what the
 * company gave as the reason, and whether it keeps coming up. No consolation, no celebration.
 */

/** The reasons that suggest something to check before the next application. The rest (position
 *  filled, team fit, other) say nothing a person could act on, so they get no tip. */
export const REJECTION_TIPS: Partial<Record<RejectionReasonCategory, "experience" | "language" | "location" | "skills" | "salary">> = {
  ExperienceLevelMismatch: "experience",
  LanguageRequirement: "language",
  LocationOrRelocation: "location",
  SkillOrTechStackGap: "skills",
  SalaryExpectationMismatch: "salary",
};

/** The stated reason on the latest move to Rejected, if the company gave one. */
export function latestRejectionReason(
  history: readonly Pick<ApplicationStatusHistoryResponse, "toStatus" | "changedAt" | "rejectionReasonCategory">[] | undefined,
): RejectionReasonCategory | null {
  const latest = [...(history ?? [])]
    .filter((entry) => entry.toStatus === "Rejected")
    .sort((a, b) => new Date(b.changedAt).getTime() - new Date(a.changedAt).getTime())[0];
  return latest && hasStatedRejectionReason(latest) ? latest.rejectionReasonCategory : null;
}
