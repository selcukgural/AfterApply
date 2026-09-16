"use client";

import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useRouter } from "@/i18n/navigation";
import { useClientConfig } from "@/hooks/useClientConfig";
import { paymentsApi } from "@/lib/api/payments";

export const PRO_QUERY_KEYS = {
  plans: ["payments", "plans"] as const,
  orders: ["payments", "orders"] as const,
  order: (id: string) => ["payments", "order", id] as const,
  billingDefaults: ["payments", "billing-defaults"] as const,
};

/**
 * The gate every /pro page passes through: the server's payments flag first (off → the page does
 * not exist, back to the dashboard), then the plans and the caller's entitlement. The query only
 * runs once the flag is known to be on, so a dark deployment never sees a 404 in the console.
 */
export function useProAccess() {
  const router = useRouter();
  const { config, isLoaded } = useClientConfig();
  const enabled = (config.payments?.enabled ?? false) && (config.jobSources?.enabled ?? false);

  useEffect(() => {
    if (isLoaded && !enabled) {
      router.replace("/dashboard");
    }
  }, [isLoaded, enabled, router]);

  const plans = useQuery({
    queryKey: PRO_QUERY_KEYS.plans,
    queryFn: paymentsApi.getPlans,
    enabled: isLoaded && enabled,
  });

  return {
    enabled: isLoaded && enabled,
    plans: plans.data ?? null,
    isLoading: !isLoaded || plans.isLoading,
    error: plans.error,
  };
}
