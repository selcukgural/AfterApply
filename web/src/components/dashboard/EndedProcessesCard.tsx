"use client";

import { useMemo } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Card } from "@/components/dashboard/Card";
import { buttonClassName } from "@/components/ui/Button";
import { useClientConfig } from "@/hooks/useClientConfig";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { contributeHref } from "@/lib/contribute/contributeState";
import { elapsedSince } from "@/lib/contributionLoop/contributionLoop";
import type { ExperienceInvite } from "@/types/api";

export const EXPERIENCE_INVITES_QUERY_KEY = ["experienceInvites"] as const;

/**
 * "Your ended processes" (contribution loop #10, canvas variant B, 2026-09-24): up to three
 * processes that closed four weeks to a year ago, at companies the person has not rated yet, each
 * one click from the rating form with the company already picked. An offer, not a nudge — the
 * standing rule the application page's closing invite follows: no count, no colour, no second ask.
 * Rating the company or pressing × takes it off for good; the API decides what is listed, this
 * only draws it. The whole card is absent when there is nothing to ask about.
 */
export function EndedProcessesCard() {
  const t = useTranslations("dashboard.endedProcesses");
  const { config } = useClientConfig();
  const enabled = config.candidateExperiences?.enabled === true;
  const queryClient = useQueryClient();

  const invites = useQuery({
    queryKey: EXPERIENCE_INVITES_QUERY_KEY,
    queryFn: candidateExperiencesApi.invites,
    enabled,
  });

  const dismiss = useMutation({
    mutationFn: (companyId: string) => candidateExperiencesApi.dismissInvite(companyId),
    // Gone at once; the refetch afterwards is what the card then shows.
    onMutate: async (companyId: string) => {
      await queryClient.cancelQueries({ queryKey: EXPERIENCE_INVITES_QUERY_KEY });
      const previous = queryClient.getQueryData<ExperienceInvite[]>(EXPERIENCE_INVITES_QUERY_KEY);
      queryClient.setQueryData<ExperienceInvite[]>(EXPERIENCE_INVITES_QUERY_KEY, (rows) =>
        (rows ?? []).filter((row) => row.companyId !== companyId),
      );
      return { previous };
    },
    onError: (_error, _companyId, context) => {
      if (context?.previous) queryClient.setQueryData(EXPERIENCE_INVITES_QUERY_KEY, context.previous);
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: EXPERIENCE_INVITES_QUERY_KEY }),
  });

  const now = useMemo(() => new Date(), []);
  const rows = invites.data ?? [];
  if (!enabled || rows.length === 0) return null;

  return (
    <Card>
      <div className="flex flex-col gap-1">
        <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</h3>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>
      <ul className="mt-3 flex flex-col divide-y divide-gray-100 dark:divide-gray-800">
        {rows.map((row) => {
          const elapsed = elapsedSince(row.endedAt, now);
          return (
            <li key={row.companyId} className="flex flex-wrap items-center gap-x-3 gap-y-2 py-2.5 first:pt-0 last:pb-0">
              <span
                aria-hidden="true"
                className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-gray-100 text-sm font-semibold text-gray-600 dark:bg-gray-800 dark:text-gray-300"
              >
                {row.companyName.charAt(0).toLocaleUpperCase()}
              </span>
              {/* A floor on the text's width: on a phone the two buttons drop to their own line
                  rather than squeezing the company's name into an ellipsis. */}
              <div className="flex min-w-[12rem] flex-1 flex-col">
                <span className="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                  {row.companyName} <span className="font-normal text-gray-500 dark:text-gray-400">· {row.jobTitle}</span>
                </span>
                <span className="text-xs text-gray-600 dark:text-gray-400">
                  {t(`outcome.${row.outcome}`)} · {t(`elapsed.${elapsed.unit}`, { count: elapsed.count })}
                </span>
              </div>
              <div className="ml-auto flex items-center gap-1">
                <Link href={contributeHref("experience", row.companySlug)} className={buttonClassName("outline", "whitespace-nowrap")}>
                  {t("rate")}
                </Link>
                <button
                  type="button"
                  onClick={() => dismiss.mutate(row.companyId)}
                  aria-label={t("dismiss", { company: row.companyName })}
                  title={t("dismiss", { company: row.companyName })}
                  className="flex h-9 w-9 items-center justify-center rounded-md text-gray-500 hover:bg-gray-100 hover:text-gray-900 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-100"
                >
                  <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </li>
          );
        })}
      </ul>
      <p className="mt-3 text-xs text-gray-500 dark:text-gray-400">{t("note")}</p>
    </Card>
  );
}
