import type { AnalyticsOverviewResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

export const analyticsApi = {
  getOverview: () => apiFetch<AnalyticsOverviewResponse>("/api/analytics/overview"),
};
