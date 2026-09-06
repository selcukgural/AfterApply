import type { ApplicationStatusHistoryResponse } from "@/types/api";

/// NotStated is the expected majority outcome for a rejection email, not an edge case (see the
/// mailbox audit in DECISIONS.md). Printing "reason: not stated" on most rejection rows would be
/// noise, so only a real stated reason earns a line.
export function hasStatedRejectionReason(
  entry: Pick<ApplicationStatusHistoryResponse, "rejectionReasonCategory">,
): boolean {
  return entry.rejectionReasonCategory !== null && entry.rejectionReasonCategory !== "NotStated";
}

/// The originating email is resolved at read time and is null once the suggestion is gone, so the
/// disclosure only appears when there is actually something behind it.
export function hasEmailContext(
  entry: Pick<ApplicationStatusHistoryResponse, "emailSubject" | "emailSnippet">,
): boolean {
  return entry.emailSubject !== null || entry.emailSnippet !== null;
}
