import type { SubmitSilenceReportRequest } from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * The anonymous "no reply" report on a company page (growth item 1.6). The path matches
 * httpClient's NO_AUTH_PATTERNS, so no Authorization header is attached even for a signed-in
 * visitor: a report is anonymous by design.
 */
export const silenceReportsApi = {
  submit: (slug: string, request: SubmitSilenceReportRequest) =>
    apiFetch<void>(`/api/companies/public/${encodeURIComponent(slug)}/silence-reports`, {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
