import type { SilenceStage, SilenceWait, SubmitSilenceReportRequest } from "@/types/api";
import { benchmarkSourceFromSearch } from "@/lib/benchmark/source";

// The anonymous "no reply" report on a company page (growth item 1.6, canvas "Son hâl — C",
// 2026-09-23). Mirrors SilenceStage / SilenceWait on the server; the server is the one that decides.

/** Form order; kept in step with the C# enum by silenceReport.test.ts. */
export const SILENCE_STAGES: readonly SilenceStage[] = [
  "AfterApplication",
  "AfterHrScreen",
  "AfterTechnicalInterview",
  "AfterFinalInterview",
  "AfterOfferTalk",
];

export const SILENCE_WAITS: readonly SilenceWait[] = ["TwoToFourWeeks", "OneToTwoMonths", "TwoToThreeMonths", "OverThreeMonths"];

export type PromiseAnswer = "yes" | "no";

export interface SilenceReportDraft {
  stage: SilenceStage | "";
  wait: SilenceWait | "";
  /** "" = not said. */
  promise: PromiseAnswer | "";
}

export const EMPTY_SILENCE_REPORT_DRAFT: SilenceReportDraft = { stage: "", wait: "", promise: "" };

/** Which required answers are missing, as keys under `companies.silenceReport.problems`. */
export function silenceReportProblems(draft: SilenceReportDraft): ("stageRequired" | "waitRequired")[] {
  const problems: ("stageRequired" | "waitRequired")[] = [];
  if (draft.stage === "") problems.push("stageRequired");
  if (draft.wait === "") problems.push("waitRequired");
  return problems;
}

/** Only call once `silenceReportProblems` is empty. The channel comes from the page's own
 *  `utm_source`, mapped onto the benchmark's closed list; the honeypot always leaves empty. */
export function buildSilenceReportRequest(draft: SilenceReportDraft, locale: string, search: string): SubmitSilenceReportRequest {
  return {
    stage: draft.stage as SilenceStage,
    wait: draft.wait as SilenceWait,
    promiseGiven: draft.promise === "" ? null : draft.promise === "yes",
    locale: locale === "tr" ? "tr" : "en",
    website: "",
    source: benchmarkSourceFromSearch(search),
  };
}
