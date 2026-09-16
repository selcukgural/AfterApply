"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { CriteriaForm, WEEKLY_JOBS_QUERY_KEYS } from "@/components/weeklyJobs/CriteriaForm";
import { ProGate } from "@/components/weeklyJobs/ProGate";
import { useWeeklyJobsAccess } from "@/components/weeklyJobs/useWeeklyJobsAccess";

export default function WeeklyJobsCriteriaPage() {
  const t = useTranslations("weeklyJobs");
  const tCommon = useTranslations("common");
  const { enabled, status, isLoading } = useWeeklyJobsAccess();

  const profile = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.profile,
    queryFn: jobSourcesApi.getProfile,
    enabled: enabled && status?.isPro === true,
  });

  if (isLoading || !status || (status.isPro && profile.isLoading)) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <Link href="/weekly-jobs" className="inline-flex items-center gap-1 text-sm text-accent-ink underline-offset-2 hover:underline">
        <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M15 6l-6 6 6 6" />
        </svg>
        {t("title")}
      </Link>
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("criteria.title")}</h1>
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("criteria.intro")}</p>
      </div>
      {status.isPro ? <CriteriaForm profile={profile.data ?? null} /> : <ProGate />}
    </div>
  );
}
