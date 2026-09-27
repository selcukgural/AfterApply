import type { FeatureFlag, FeatureFlagResponse } from "@/types/api";
import { ApiError } from "@/lib/api/httpClient";

/** Every flag, in the order the panel lists them. `featureFlags.test.ts` checks this against the
 *  API's enum, so a flag added there cannot go missing here. */
export const FEATURE_FLAGS = [
  "Board",
  "EmailSignals",
  "EmailAutoApproval",
  "AtsSources",
  "JobLiveness",
  "CompanyReviews",
  "CompanySalaries",
  "CandidateExperiences",
  "SilenceReports",
  "CompanyIntelligence",
  "ResponseRates",
  "Blog",
  "CvScan",
  "CvScanNotes",
  "FeedbackGitHub",
  "JobSources",
  "Payments",
] as const satisfies readonly FeatureFlag[];

// Compile-time: a member of the FeatureFlag type that the list above lacks is an error here.
type Unlisted = Exclude<FeatureFlag, (typeof FEATURE_FLAGS)[number]>;
const everyFlagListed: Unlisted extends never ? true : never = true;
void everyFlagListed;

export type FlagGroupKey = "tracking" | "companies" | "content" | "pro";

/** The panel's four groups (canvas "Özellik bayrakları paneli", variant C, 2026-09-27). */
export const FLAG_GROUPS: readonly { key: FlagGroupKey; flags: readonly FeatureFlag[] }[] = [
  { key: "tracking", flags: ["Board", "EmailSignals", "EmailAutoApproval", "AtsSources", "JobLiveness"] },
  { key: "companies", flags: ["CompanyReviews", "CompanySalaries", "CandidateExperiences", "SilenceReports", "CompanyIntelligence", "ResponseRates"] },
  { key: "content", flags: ["Blog", "CvScan", "CvScanNotes", "FeedbackGitHub"] },
  { key: "pro", flags: ["JobSources", "Payments"] },
];

/** What a switch does: set the other state, or drop the panel's value (back to the deploy default). */
export type ChangeKind = "turnOn" | "turnOff" | "reset";

/** The override to send for a change: `null` removes it. */
export function targetOf(kind: ChangeKind): boolean | null {
  return kind === "reset" ? null : kind === "turnOn";
}

/** The main action for a flag: the opposite of what it does now. */
export function toggleKind(flag: Pick<FeatureFlagResponse, "enabled">): ChangeKind {
  return flag.enabled ? "turnOff" : "turnOn";
}

/** Whether the panel's value can be dropped — only when there is one. */
export function canReset(flag: Pick<FeatureFlagResponse, "override">): boolean {
  return flag.override !== null;
}

/** Switching on is refused while the deployment lacks what the flag needs; off and reset never are. */
export function isBlocked(flag: Pick<FeatureFlagResponse, "missingPrerequisite">, kind: ChangeKind): boolean {
  return kind === "turnOn" && flag.missingPrerequisite !== null;
}

/** The second step's typed confirmation: the flag's name exactly, case included (the API's rule). */
export function phraseMatches(typed: string, phrase: string): boolean {
  return typed.trim() === phrase;
}

/** A few seconds short of the server's own expiry: the answer took time to arrive, and the
 *  confirmation takes time to travel back. Better to ask again than to fail at the last second. */
const DEADLINE_MARGIN_MS = 5_000;

/** When the token stops working, on this browser's clock: the lifetime counted from the moment
 *  the first step's answer arrived. Never the server's `expiresAt` against the local clock — an
 *  admin's clock five minutes fast would make every confirmation look expired on arrival. */
export function localDeadline(receivedAt: number, expiresInSeconds: number): number {
  return receivedAt + Math.max(0, expiresInSeconds * 1000 - DEADLINE_MARGIN_MS);
}

/** Whole seconds until `deadline`, never negative. */
export function secondsLeft(deadline: number, now: number): number {
  return Math.max(0, Math.floor((deadline - now) / 1000));
}

/**
 * Whether the change the dialog was opened for no longer changes anything — another admin made it
 * meanwhile (the list was re-read after a 409). The dialog then says so instead of offering a
 * first step whose answer can only be "nothing to change".
 */
export function isOvertaken(kind: ChangeKind, flag: Pick<FeatureFlagResponse, "enabled" | "override">): boolean {
  if (kind === "reset") return flag.override === null;
  return (kind === "turnOn") === flag.enabled;
}

/** 272 → "4:32". */
export function formatCountdown(seconds: number): string {
  const safe = Math.max(0, Math.floor(seconds));
  return `${Math.floor(safe / 60)}:${String(safe % 60).padStart(2, "0")}`;
}

/** The `code` a 409 from the flag endpoints carries, if any. */
export function problemCode(error: unknown): string | null {
  if (!(error instanceof ApiError) || !error.body || typeof error.body !== "object") {
    return null;
  }
  const code = (error.body as { code?: unknown }).code;
  return typeof code === "string" ? code : null;
}

/** Answers after which the confirmation cannot be retried as it is: the token is spent, expired
 *  or overtaken by another change, or switching on became impossible — the dialog goes back to
 *  the first step with fresh data. */
export function mustStartAgain(error: unknown): boolean {
  const code = problemCode(error);
  return (
    code === "FEATURE_FLAG_CHANGED_SINCE_PREPARE" ||
    code === "FEATURE_FLAG_CONFIRMATION_INVALID" ||
    // The configuration a switch-on needs went missing between the steps: the first step says why.
    code === "FEATURE_FLAG_PREREQUISITE_MISSING"
  );
}
