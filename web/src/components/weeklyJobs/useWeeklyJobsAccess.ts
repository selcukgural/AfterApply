"use client";

import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "@/i18n/navigation";
import { useClientConfig } from "@/hooks/useClientConfig";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { WEEKLY_JOBS_QUERY_KEYS } from "./CriteriaForm";

/**
 * The gate every weekly-jobs page passes through: the server flag first (off → the page does
 * not exist, back to the dashboard), then the account's status. The status query only runs
 * once the flag is known to be on, so a dark deployment never sees a 404 in the console.
 */
export function useWeeklyJobsAccess() {
  const router = useRouter();
  const { config, isLoaded } = useClientConfig();
  const enabled = config.jobSources?.enabled ?? false;

  useEffect(() => {
    if (isLoaded && !enabled) {
      router.replace("/dashboard");
    }
  }, [isLoaded, enabled, router]);

  const status = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.status,
    queryFn: jobSourcesApi.getStatus,
    enabled: isLoaded && enabled,
  });

  return { enabled: isLoaded && enabled, status: status.data ?? null, isLoading: !isLoaded || status.isLoading };
}
