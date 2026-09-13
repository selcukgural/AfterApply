import type {
  CompanyReviewRequest,
  CompanyReviewViewerState,
  HelpfulToggleResponse,
  MyCompanyReview,
  MyReviewsResponse,
  ReportCompanyReviewRequest,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** Everything a signed-in person does with reviews. A review that is not the caller's answers 404
 *  on every write, never 403 — the server does not confirm what exists. */
export const companyReviewsApi = {
  viewerState: (companyId: string) =>
    apiFetch<CompanyReviewViewerState>(`/api/companies/${companyId}/reviews/me`),

  create: (companyId: string, request: CompanyReviewRequest) =>
    apiFetch<MyCompanyReview>(`/api/companies/${companyId}/reviews`, { method: "POST", body: JSON.stringify(request) }),

  update: (reviewId: string, request: CompanyReviewRequest) =>
    apiFetch<MyCompanyReview>(`/api/company-reviews/${reviewId}`, { method: "PUT", body: JSON.stringify(request) }),

  remove: (reviewId: string) => apiFetch<void>(`/api/company-reviews/${reviewId}`, { method: "DELETE" }),

  listMine: () => apiFetch<MyReviewsResponse>("/api/company-reviews/mine"),

  toggleHelpful: (reviewId: string) =>
    apiFetch<HelpfulToggleResponse>(`/api/company-reviews/${reviewId}/helpful`, { method: "POST" }),

  report: (reviewId: string, request: ReportCompanyReviewRequest) =>
    apiFetch<{ id: string; reportedAt: string }>(`/api/company-reviews/${reviewId}/reports`, {
      method: "POST",
      body: JSON.stringify(request),
    }),
};
