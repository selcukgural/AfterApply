import type { AutoApprovalCalibrationResponse, ProductMetricsDayResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const adminApi = {
  /** Stored product metrics, newest day first. 403 for any account without Users.IsAdmin. */
  getMetrics: (days = 30) => apiFetch<ProductMetricsDayResponse[]>(`/api/admin/metrics?days=${days}`),

  /** Auto-approval accuracy by confidence band — the evidence for choosing the threshold. */
  getAutoApprovalCalibration: () =>
    apiFetch<AutoApprovalCalibrationResponse>("/api/admin/auto-approval-calibration"),
};
