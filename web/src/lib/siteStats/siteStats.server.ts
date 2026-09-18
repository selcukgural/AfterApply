// Server-side only by convention (called from server components), like publicConfig.server.ts.
import type { SiteStatsResponse } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

/** An hour, the same as the API's own cache: the figures are running totals, not live ones. */
const REVALIDATE_SECONDS = 3600;

/**
 * GET /api/site-stats for the landing page's strip and the about page. Null when the API is
 * unreachable (build time, an outage): both pages then render without the figures, which is
 * exactly what they do while every figure is under the threshold.
 */
export async function fetchSiteStats(): Promise<SiteStatsResponse | null> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/site-stats`, {
      headers: { Accept: "application/json" },
      next: { revalidate: REVALIDATE_SECONDS },
    });
    if (!response.ok) return null;
    return (await response.json()) as SiteStatsResponse;
  } catch {
    return null;
  }
}
