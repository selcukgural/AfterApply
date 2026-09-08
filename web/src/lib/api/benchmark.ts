import type {
  BenchmarkResultResponse,
  BenchmarkSummaryResponse,
  SubmitBenchmarkRequest,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * The public benchmark. Goes through `apiFetch` — unlike the visit counter — because this form has
 * errors worth showing a person ("replies cannot exceed applications"), and apiFetch is what reads
 * a ValidationProblem's `errors` and surfaces the localised message.
 *
 * `/api/benchmark` is on httpClient's NO_AUTH_ENDPOINTS list, so no Authorization header is
 * attached even when the visitor happens to be signed in: an answer here is anonymous by design,
 * and a signed-in one arriving with a token would not be.
 */
export const benchmarkApi = {
  submit: (request: SubmitBenchmarkRequest) =>
    apiFetch<BenchmarkResultResponse>("/api/benchmark/submissions", {
      method: "POST",
      body: JSON.stringify(request),
    }),

  getSummary: () => apiFetch<BenchmarkSummaryResponse>("/api/benchmark/summary"),
};
