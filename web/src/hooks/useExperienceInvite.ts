"use client";

import { useQuery } from "@tanstack/react-query";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { useClientConfig } from "@/hooks/useClientConfig";

/**
 * Whether an application page may offer the candidate experience form for this company right
 * now: the feature is on, the company has a public page to send the person to, and they have not
 * already written one. `wanted` is the caller's own condition (the status is a closing one) so
 * that the viewer-state request is only made when the line could show at all — a fresh
 * application never fires it.
 */
export function useExperienceInvite(companyId: string, companySlug: string | null | undefined, wanted: boolean): boolean {
  const { config } = useClientConfig();
  const enabled = config.candidateExperiences?.enabled === true && wanted && !!companySlug;

  const viewer = useQuery({
    queryKey: ["companies", companyId, "experienceViewer"],
    queryFn: () => candidateExperiencesApi.viewerState(companyId),
    enabled,
  });

  return enabled && !!viewer.data && viewer.data.ownEntry === null;
}
