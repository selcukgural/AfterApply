"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CompanyPublicResponse } from "@/types/api";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { useAuth } from "@/lib/auth/AuthContext";
import { contributeHref } from "@/lib/contribute/contributeState";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { ExperienceCard } from "@/components/candidateExperiences/ExperienceCard";
import { HelpfulPill } from "@/components/contributions/HelpfulPill";
import { ApiError } from "@/lib/api/httpClient";
import { ExperienceSummaryPanel } from "@/components/candidateExperiences/ExperienceSummaryPanel";

/**
 * The candidate-experience tab of a company page. Public, like the reviews: the list is fetched
 * for anyone (the endpoint is anonymous), a signed-in reader additionally sees their own entry
 * and a "share" button, and a visitor is sent to sign in and back to this tab when they want to
 * add theirs. Not in the cached page — the reviews tab is the server-rendered half.
 */
export function CandidateExperiencesPanel({ company }: { company: CompanyPublicResponse }) {
  const t = useTranslations("candidateExperiences.panel");
  const { isAuthenticated } = useAuth();
  const [page, setPage] = useState(1);

  const listQuery = useQuery({
    queryKey: ["companies", company.slug, "experiences", { page }],
    queryFn: () => candidateExperiencesApi.list(company.slug, page),
  });
  const viewerQuery = useQuery({
    queryKey: ["companies", company.id, "experienceViewer"],
    queryFn: () => candidateExperiencesApi.viewerState(company.id),
    enabled: isAuthenticated,
  });

  const queryClient = useQueryClient();
  const [helpfulError, setHelpfulError] = useState<string | null>(null);
  const helpful = useMutation({
    mutationFn: (experienceId: string) => candidateExperiencesApi.toggleHelpful(experienceId),
    onMutate: () => setHelpfulError(null),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: ["companies", company.slug, "experiences"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", company.id, "experienceViewer"] }),
      ]),
    onError: (error) => setHelpfulError(error instanceof ApiError ? error.message : t("helpfulError")),
  });

  const shareHref = contributeHref("experience", company.slug);
  const returnTo = `/companies/${company.slug}?tab=experiences`;
  const signInHref = `/login?next=${encodeURIComponent(returnTo)}`;
  const list = listQuery.data;
  const own = viewerQuery.data;
  const ownEntry = own?.ownEntry ?? null;
  const marked = new Set(own?.helpfulMarkedExperienceIds ?? []);
  const quotaLeft = own ? Math.max(0, own.quota.limit - own.quota.used) : 0;
  const canShare = !isAuthenticated || (own !== undefined && ownEntry === null && quotaLeft > 0);
  const count = list?.total ?? company.candidateExperienceCount ?? 0;

  const shareLink = (variant: "outline" | "primary", label: string) =>
    isAuthenticated ? (
      <Link href={shareHref} className={buttonClassName(variant)}>
        {label}
      </Link>
    ) : (
      <a href={signInHref} className={buttonClassName(variant)}>
        {t("signInToShare")}
      </a>
    );

  return (
    <section className="flex flex-col gap-4">
      {list ? <ExperienceSummaryPanel summary={list.summary} /> : null}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("heading", { count })}</h2>
        {canShare ? shareLink("outline", t("share")) : null}
      </div>

      <p className="text-xs text-gray-600 dark:text-gray-400">{t("readingNote")}</p>

      {ownEntry ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-accent/40 bg-accent-wash p-4 text-sm">
          <div className="flex flex-col gap-1">
            <span className="font-medium text-gray-900 dark:text-gray-100">{t("yours")}</span>
            <span className="text-gray-700 dark:text-gray-300">{t("ownSummary", { value: ownEntry.overallRating })}</span>
          </div>
          <Link href={`/my-experiences/${ownEntry.id}/edit`} className={buttonClassName("outline")}>
            {t("editYours")}
          </Link>
        </div>
      ) : null}

      {listQuery.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {listQuery.isError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {t("loadError")}
        </p>
      )}

      {list && list.items.length === 0 && (
        <div className="flex flex-col items-start gap-3 rounded-xl border border-dashed border-gray-300 p-8 text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{t("empty")}</p>
          {canShare ? shareLink("primary", t("emptyCta")) : null}
        </div>
      )}

      {helpfulError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {helpfulError}
        </p>
      )}

      {list && list.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {list.items.map((entry) => (
            <li key={entry.id}>
              <ExperienceCard
                experience={entry}
                footer={
                  <HelpfulPill
                    count={entry.helpfulCount}
                    marked={marked.has(entry.id)}
                    busy={helpful.isPending}
                    own={ownEntry?.id === entry.id}
                    onToggle={isAuthenticated ? () => helpful.mutate(entry.id) : undefined}
                    signInHref={signInHref}
                  />
                }
              />
            </li>
          ))}
        </ul>
      )}

      {list && list.items.length > 0 && !ownEntry && canShare ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-gray-50 p-4 text-sm dark:border-gray-800 dark:bg-gray-900/60">
          <span className="text-gray-700 dark:text-gray-300">{t("cta")}</span>
          {shareLink("primary", t("share"))}
        </div>
      ) : null}

      {list && <Pagination page={list.page} pageSize={list.pageSize} totalCount={list.total} unit="experiences" onPageChange={setPage} />}
    </section>
  );
}
