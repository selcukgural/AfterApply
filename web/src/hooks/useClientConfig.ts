"use client";

import { useQuery } from "@tanstack/react-query";
import { configApi, DEFAULT_CLIENT_CONFIG } from "@/lib/api/config";
import type { ClientConfigResponse } from "@/types/api";

export const CLIENT_CONFIG_QUERY_KEY = ["client-config"] as const;

/**
 * The server's public limits (password policy, access-token limits). Never suspends and never
 * errors out the caller: until the request resolves — or if it fails — `config` is the built-in
 * default, which matches the API's own appsettings defaults.
 */
export function useClientConfig(): { config: ClientConfigResponse; isLoaded: boolean } {
  const query = useQuery({
    queryKey: CLIENT_CONFIG_QUERY_KEY,
    queryFn: configApi.get,
    // A minute, the API's own max-age: the feature flags in here switch at runtime from the admin
    // panel (2026-09-27), and a switched-off feature's links should leave within that.
    staleTime: 60_000,
    gcTime: 30 * 60_000,
  });

  return { config: query.data ?? DEFAULT_CLIENT_CONFIG, isLoaded: query.data !== undefined };
}
