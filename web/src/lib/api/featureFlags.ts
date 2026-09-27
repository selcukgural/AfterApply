import type {
  FeatureFlag,
  FeatureFlagChangeResponse,
  FeatureFlagResponse,
  PrepareFeatureFlagChangeResponse,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/**
 * The admin side of the runtime feature flags. A switch is two calls on purpose: `prepare` changes
 * nothing and returns a short-lived token; `confirm` applies it only with that token and the flag's
 * name typed out. `enabled: null` removes the override (back to the deploy default).
 */
export const featureFlagsApi = {
  list: () => apiFetch<FeatureFlagResponse[]>("/api/admin/feature-flags"),

  history: (flag?: FeatureFlag, limit = 50) =>
    apiFetch<FeatureFlagChangeResponse[]>(
      `/api/admin/feature-flags/history?limit=${limit}${flag ? `&flag=${encodeURIComponent(flag)}` : ""}`,
    ),

  prepare: (flag: FeatureFlag, enabled: boolean | null, reason: string) =>
    apiFetch<PrepareFeatureFlagChangeResponse>(`/api/admin/feature-flags/${encodeURIComponent(flag)}/prepare`, {
      method: "POST",
      body: JSON.stringify({ enabled, reason }),
    }),

  confirm: (flag: FeatureFlag, confirmationToken: string, confirmationPhrase: string) =>
    apiFetch<FeatureFlagResponse>(`/api/admin/feature-flags/${encodeURIComponent(flag)}/confirm`, {
      method: "POST",
      body: JSON.stringify({ confirmationToken, confirmationPhrase }),
    }),
};
