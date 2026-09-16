"use client";

import { useQuery } from "@tanstack/react-query";
import { WEEKLY_JOBS_QUERY_KEYS } from "@/lib/weeklyJobs/queryKeys";
import { useClientConfig } from "@/hooks/useClientConfig";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { canSeeProNav } from "@/lib/payments/proNav";

/**
 * Whether the weekly-postings item in the navigation should carry the "Pro" badge: the plan is
 * on sale (both server flags) and this account is not Pro yet. The same status query the
 * weekly-jobs pages use, so a purchase reaches the badge as soon as the result page invalidates
 * it; it never fires while the feature is dark.
 */
export function useProBadge(): { weeklyJobsEnabled: boolean; showProBadge: boolean } {
  const { config, isLoaded } = useClientConfig();
  const weeklyJobsEnabled = isLoaded && config.jobSources?.enabled === true;

  const status = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.status,
    queryFn: jobSourcesApi.getStatus,
    enabled: weeklyJobsEnabled,
    staleTime: 5 * 60_000,
  });

  return { weeklyJobsEnabled, showProBadge: canSeeProNav(config) && status.data?.isPro === false };
}
