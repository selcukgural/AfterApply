import type {
  AdminCompanyReview,
  AdminCompanyReviewListItem,
  AdminReviewReport,
  AutoApprovalCalibrationResponse,
  ModerationCounts,
  PagedResult,
  ProductMetricsDayResponse,
  ReviewReportResolution,
  ReviewReportStatus,
  SiteTrafficCounterResponse,
  UserReviewQuota,
} from "@/types/api";
import type { ModerationListFilters } from "@/lib/companyReviews/moderationListView";
import { buildModerationQueryString } from "@/lib/companyReviews/moderationListView";
import { apiFetch } from "./httpClient";

export const adminApi = {
  /** Stored product metrics, newest day first. 403 for any account without Users.IsAdmin. */
  getMetrics: (days = 30) => apiFetch<ProductMetricsDayResponse[]>(`/api/admin/metrics?days=${days}`),

  /** Public-site visit counts, newest day first. Aggregate only — no visitor is identified. */
  getSiteTraffic: (days = 30) =>
    apiFetch<SiteTrafficCounterResponse[]>(`/api/admin/site-traffic?days=${days}`),

  /** Auto-approval accuracy by confidence band — the evidence for choosing the threshold. */
  getAutoApprovalCalibration: () =>
    apiFetch<AutoApprovalCalibrationResponse>("/api/admin/auto-approval-calibration"),

  // Company-review moderation. The only surface where a review's author is joined to a response.
  listReviews: (filters: ModerationListFilters) =>
    apiFetch<PagedResult<AdminCompanyReviewListItem>>(`/api/admin/company-reviews${buildModerationQueryString(filters)}`),

  getModerationCounts: () => apiFetch<ModerationCounts>("/api/admin/company-reviews/counts"),

  getReview: (reviewId: string) => apiFetch<AdminCompanyReview>(`/api/admin/company-reviews/${reviewId}`),

  approveReview: (reviewId: string) =>
    apiFetch<void>(`/api/admin/company-reviews/${reviewId}/approve`, { method: "POST" }),

  rejectReview: (reviewId: string, reason: string) =>
    apiFetch<void>(`/api/admin/company-reviews/${reviewId}/reject`, { method: "POST", body: JSON.stringify({ reason }) }),

  listReports: (status: ReviewReportStatus, page: number) =>
    apiFetch<PagedResult<AdminReviewReport>>(`/api/admin/company-review-reports?status=${status}&page=${page}`),

  resolveReport: (reportId: string, resolution: ReviewReportResolution, reason: string | null) =>
    apiFetch<void>(`/api/admin/company-review-reports/${reportId}/resolve`, {
      method: "POST",
      body: JSON.stringify({ resolution, reason }),
    }),

  setReviewQuota: (userId: string, reviewQuotaOverride: number | null) =>
    apiFetch<UserReviewQuota>(`/api/admin/users/${userId}/review-quota`, {
      method: "PUT",
      body: JSON.stringify({ reviewQuotaOverride }),
    }),
};
