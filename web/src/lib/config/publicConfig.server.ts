// Server-side only by convention (called from server components); holds no secret, same as
// companies/publicApi.server.ts, which this follows.
import type { ClientConfigResponse } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

/** A minute, like the public company pages: a flag flip shows within that, a crawler never pins the API. */
const REVALIDATE_SECONDS = 60;

/**
 * GET /api/config from the server, for the one place a flag decides what the HTML says before any
 * script runs: the landing page's first screen. The browser reads the same document through
 * useClientConfig; this exists because a hero that swaps after hydration is a hero that jumps.
 * Null when the API is unreachable (build time, an outage) — the caller renders the flag-off page.
 */
export async function fetchPublicConfig(): Promise<ClientConfigResponse | null> {
  try {
    const response = await fetch(`${API_BASE_URL}/api/config`, {
      headers: { Accept: "application/json" },
      next: { revalidate: REVALIDATE_SECONDS },
    });
    if (!response.ok) {
      return null;
    }
    return (await response.json()) as ClientConfigResponse;
  } catch {
    return null;
  }
}

/** Whether the weekly postings exist on this deployment — what decides the landing page's hero. */
export async function fetchJobSourcesEnabled(): Promise<boolean> {
  const config = await fetchPublicConfig();
  return config?.jobSources?.enabled === true;
}
