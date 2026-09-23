import type { AnalyticsOverviewResponse, ApplicationFlowResponse, FlowPeriod } from "@/types/api";
import { apiFetch } from "./httpClient";

export const analyticsApi = {
  getOverview: () => apiFetch<AnalyticsOverviewResponse>("/api/analytics/overview"),
  getFlow: (period: FlowPeriod) =>
    apiFetch<ApplicationFlowResponse>(`/api/analytics/flow?period=${encodeURIComponent(period)}`),
};
