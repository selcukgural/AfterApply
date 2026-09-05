import type { ClientConfigResponse } from "@/types/api";
import { apiFetch } from "./httpClient";

// Mirrors the API's appsettings.json defaults. Used only while /api/config hasn't answered (or
// failed): the forms must still work, and the server re-validates everything anyway, so a stale
// fallback costs at worst one extra round-trip with the server's own message — never a wrong
// acceptance.
export const DEFAULT_CLIENT_CONFIG: ClientConfigResponse = {
  passwordPolicy: {
    requiredLength: 12,
    requiredUniqueChars: 4,
    requireDigit: true,
    requireLowercase: true,
    requireUppercase: true,
    requireNonAlphanumeric: true,
  },
  personalAccessTokens: {
    maxActiveTokens: 10,
    lifetimeDays: 90,
  },
  // Off until the server says otherwise: the Google button must never render against a
  // deployment that has no client configured (its endpoints answer 404 there).
  googleAuth: {
    enabled: false,
    clientId: null,
  },
};

export const configApi = {
  get: () => apiFetch<ClientConfigResponse>("/api/config"),
};
