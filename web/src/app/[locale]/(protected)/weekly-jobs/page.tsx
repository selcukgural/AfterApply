"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { weekKeyToMonday, weekNumber } from "@/lib/weeklyJobs/score";
import { CriteriaForm, WEEKLY_JOBS_QUERY_KEYS } from "@/components/weeklyJobs/CriteriaForm";
import { CriteriaStrip } from "@/components/weeklyJobs/CriteriaStrip";
import { PostingDetail } from "@/components/weeklyJobs/PostingDetail";
import { PostingList } from "@/components/weeklyJobs/PostingList";
import { ProGate } from "@/components/weeklyJobs/ProGate";
import { useWeeklyJobsAccess } from "@/components/weeklyJobs/useWeeklyJobsAccess";
import { EmptyState } from "@/components/ui/EmptyState";

/**
 * The weekly postings, direction A of the 2026-09-14 canvas: the list on the left, the selected
 * posting on the right, first one open. Below md the right column is not rendered and a row is
 * a link to the posting's own page (PostingList). Three states before the list: the flag off
 * (redirect), not on the paid plan (ProGate), no criteria yet (the form inline).
 */
export default function WeeklyJobsPage() {
  const t = useTranslations("weeklyJobs");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const { enabled, status, isLoading } = useWeeklyJobsAccess();
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const profile = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.profile,
    queryFn: jobSourcesApi.getProfile,
    enabled: enabled && status?.isPro === true,
  });

  const postings = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.postings(),
    queryFn: () => jobSourcesApi.listPostings(),
    enabled: enabled && status?.isPro === true && status.hasProfile,
  });

  // The first row opens by itself on a desktop, and a selection that fell out of the list (a
  // refetch after "I applied") falls back to it — derived, not synced, so there is no
  // render-then-correct flicker.
  const items = postings.data?.items ?? [];
  const effectiveId = items.some((item) => item.id === selectedId) ? selectedId : (items[0]?.id ?? null);

  const detail = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.posting(effectiveId ?? ""),
    queryFn: () => jobSourcesApi.getPosting(effectiveId!),
    enabled: effectiveId !== null,
  });

  if (isLoading || !status) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>;
  }

  if (!status.isPro) {
    return (
      <div className="flex flex-col gap-6">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <ProGate />
      </div>
    );
  }

  if (!status.hasProfile) {
    return (
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-1">
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("criteria.title")}</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("criteria.intro")}</p>
        </div>
        <CriteriaForm profile={null} />
      </div>
    );
  }

  const run = postings.data?.run ?? null;
  const subtitle = run
    ? t("subtitle", {
        week: weekNumber(run.weekKey),
        date: weekKeyToMonday(run.weekKey).toLocaleDateString(locale, { day: "numeric", month: "long" }),
        found: run.deliveredCount,
        shown: items.length,
      })
    : null;

  const hiddenNote = run
    ? [
        run.excludedAppliedCount > 0 ? t("hidden.applied", { count: run.excludedAppliedCount }) : null,
        run.hiddenBelowMinScoreCount > 0
          ? t("hidden.belowMinScore", { count: run.hiddenBelowMinScoreCount, score: profile.data?.minScore ?? 0 })
          : null,
      ].filter((part): part is string => part !== null)
    : [];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        {subtitle && <p className="text-sm text-gray-500 dark:text-gray-400">{subtitle}</p>}
      </div>

      {!status.hasCv && (
        <p className="rounded-lg border border-warn/40 bg-warn-wash px-4 py-3 text-sm text-warn-ink">
          {t("noCv")}{" "}
          <Link href="/cv" className="font-medium underline-offset-2 hover:underline">
            {t("noCvLink")}
          </Link>
        </p>
      )}

      {profile.data && <CriteriaStrip profile={profile.data} />}

      {postings.isLoading ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : items.length === 0 ? (
        <EmptyState title={t("empty.title")} body={run ? t("empty.afterRun") : t("empty.beforeRun")} />
      ) : (
        <div className="grid items-start gap-4 md:grid-cols-[400px_minmax(0,1fr)]">
          <div className="flex flex-col gap-2">
            <PostingList items={items} selectedId={effectiveId} onSelect={setSelectedId} />
            {hiddenNote.length > 0 && <p className="text-xs text-gray-500 dark:text-gray-400">{hiddenNote.join(" ")}</p>}
          </div>
          <div className="hidden md:block">
            {detail.data ? (
              <PostingDetail posting={detail.data} />
            ) : (
              <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
