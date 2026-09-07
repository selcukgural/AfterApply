import type { ApplicationEventType } from "@/types/api";

/**
 * The event types a person can add by hand.
 *
 * `ApplicationCreated`, `ApplicationSubmitted` and `StatusChanged` exist in the API's enum but are
 * left out on purpose: they describe things the product records for itself, and offering them here
 * would let a manual note claim a status change that never happened. Status moves through the
 * status control, which writes real history.
 */
export const MANUAL_APPLICATION_EVENT_TYPES: ApplicationEventType[] = [
  "RecruiterContacted",
  "ScreeningStarted",
  "InterviewScheduled",
  "InterviewCompleted",
  "OfferReceived",
  "FollowUpSent",
];
