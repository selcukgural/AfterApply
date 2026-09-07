import type { ProductMetricsDayResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const adminApi = {
  /** Stored product metrics, newest day first. 403 for any account without Users.IsAdmin. */
  getMetrics: (days = 30) => apiFetch<ProductMetricsDayResponse[]>(`/api/admin/metrics?days=${days}`),
};
