import type {
  AutoApprovalCalibrationResponse,
  ProductMetricsDayResponse,
  SiteTrafficCounterResponse,
} from "@/types/api";
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
};
