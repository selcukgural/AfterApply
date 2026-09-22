import type { BenchmarkResultResponse } from "@/types/api";

/**
 * The survey framing of a benchmark result that cannot yet be compared within its own sector
 * (growth research 2026-09-21, item 0.1; canvas variant B). Nothing here lowers a threshold or
 * invents a number: every figure is one the API already returned, rearranged into "what opens
 * next, and how far off it is".
 */

export interface SurveyStep {
  count: number;
  minimum: number;
  reached: boolean;
  /** 0–100, for the progress bar's width; capped so an over-full pool never overflows. */
  percent: number;
}

export interface SurveyProgress {
  /** The answerer's place in the survey — everyone who has answered, this answer included. */
  participant: number;
  /** Step 1: the median across every field. */
  overall: SurveyStep;
  /** Step 2: the median of the answerer's own field. */
  sector: SurveyStep;
}

function step(count: number, minimum: number): SurveyStep {
  const reached = count >= minimum;
  const percent = minimum <= 0 ? 100 : Math.min(100, Math.round((count / minimum) * 100));
  return { count, minimum, reached, percent };
}

/**
 * `null` once the sector stands on its own: the page is then the ordinary comparison it was built
 * to be, and a progress panel beside a real median would only be noise.
 */
export function surveyProgress(result: BenchmarkResultResponse): SurveyProgress | null {
  if (result.scope === "Sector") return null;

  return {
    participant: result.totalSubmissions,
    overall: step(result.totalSubmissions, result.minimumSampleSize),
    sector: step(result.sampleSize, result.minimumSampleSize),
  };
}
