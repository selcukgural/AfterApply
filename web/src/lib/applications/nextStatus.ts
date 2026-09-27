import type { ApplicationStatus } from "@/types/api";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";

/** The forward path an application walks; the statuses after it end the process. */
const PIPELINE: readonly ApplicationStatus[] = [
  "Applied",
  "Screening",
  "Interview",
  "TechnicalInterview",
  "FinalInterview",
  "Offer",
  "Accepted",
];

/**
 * The status the "change status" picker opens on: the next step forward, which is what someone
 * moving an application along almost always wants. It used to open on the first status in list
 * order, which for an application already in an interview was a step backwards. An ended process
 * has no "next", so it keeps the first other status.
 */
export function suggestedNextStatus(current: ApplicationStatus): ApplicationStatus {
  const index = PIPELINE.indexOf(current);
  if (index >= 0 && index < PIPELINE.length - 1) return PIPELINE[index + 1];
  return APPLICATION_STATUSES.find((s) => s !== current) ?? current;
}
