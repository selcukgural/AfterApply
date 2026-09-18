"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { useClientConfig } from "@/hooks/useClientConfig";
import { invitesExperience } from "@/lib/applications/shareExperience";
import type { ApplicationStatus } from "@/types/api";

interface ShareExperienceInviteProps {
  status: ApplicationStatus;
  companyId: string;
  companyName: string;
  companySlug: string | null | undefined;
}

/**
 * One quiet line under a closed application — rejected or ghosted — offering the candidate
 * experience form with the company already picked (DEVELOPMENT_PLAN.md, T-series, T6).
 *
 * The moment matters: a process that just ended badly is the one thing this person knows that
 * the next candidate cannot find out anywhere else, and putting it on record is the difference
 * between "that was for nothing" and "that was for someone". No box, no accent colour, no
 * exclamation mark — it is an offer, not a prompt, and it disappears once they have taken it.
 */
export function ShareExperienceInvite({ status, companyId, companyName, companySlug }: ShareExperienceInviteProps) {
  const t = useTranslations("applications.detail.shareExperience");
  const { config } = useClientConfig();
  const enabled = config.candidateExperiences?.enabled === true && invitesExperience(status) && !!companySlug;

  // Only asked when the line could show at all — a fresh application never fires this request.
  const viewer = useQuery({
    queryKey: ["companies", companyId, "experienceViewer"],
    queryFn: () => candidateExperiencesApi.viewerState(companyId),
    enabled,
  });

  if (!enabled || !viewer.data || viewer.data.ownEntry !== null) return null;

  return (
    <p className="text-sm text-gray-600 dark:text-gray-400">
      {t("text")}{" "}
      <Link
        href={`/contribute?tab=experience&company=${encodeURIComponent(companySlug!)}`}
        className="font-medium text-gray-900 hover:underline dark:text-gray-100"
      >
        {t("link", { company: companyName })}
      </Link>
    </p>
  );
}
