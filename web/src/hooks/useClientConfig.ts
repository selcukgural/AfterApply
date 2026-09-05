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
    // Changes only with a config rollout; no reason to refetch per form mount.
    staleTime: 5 * 60_000,
    gcTime: 30 * 60_000,
  });

  return { config: query.data ?? DEFAULT_CLIENT_CONFIG, isLoaded: query.data !== undefined };
}
