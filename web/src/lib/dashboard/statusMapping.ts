/**
 * Dashboard tile definitions. Counts come from GET /api/applications/summary
 * (backend computes the same Active/Waiting/Interviews bucket math — see
 * ApplicationService.GetSummaryCountsAsync and DECISIONS.md's Sprint 2 section
 * for the status-to-bucket mapping this mirrors).
 */
import type { ApplicationSummaryCountsResponse } from "@/types/api";

export interface DashboardTile {
  key: keyof ApplicationSummaryCountsResponse;
}

export const DASHBOARD_TILES: DashboardTile[] = [
  { key: "total" },
  { key: "active" },
  { key: "waiting" },
  { key: "interviews" },
  { key: "offers" },
  { key: "rejected" },
  { key: "ghosted" },
];
