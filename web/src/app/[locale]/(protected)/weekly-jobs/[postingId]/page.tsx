"use client";

import { use } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { jobSourcesApi } from "@/lib/api/jobSources";
import { WEEKLY_JOBS_QUERY_KEYS } from "@/components/weeklyJobs/CriteriaForm";
import { PostingDetail } from "@/components/weeklyJobs/PostingDetail";
import { useWeeklyJobsAccess } from "@/components/weeklyJobs/useWeeklyJobsAccess";
import { EmptyState } from "@/components/ui/EmptyState";

/** One posting on its own page — where a phone lands from the list, and a shareable-with-yourself URL. */
export default function WeeklyJobPostingPage({ params }: { params: Promise<{ postingId: string }> }) {
  const { postingId } = use(params);
  const t = useTranslations("weeklyJobs");
  const tCommon = useTranslations("common");
  const { enabled } = useWeeklyJobsAccess();

  const posting = useQuery({
    queryKey: WEEKLY_JOBS_QUERY_KEYS.posting(postingId),
    queryFn: () => jobSourcesApi.getPosting(postingId),
    enabled,
    retry: false,
  });

  return (
    <div className="flex flex-col gap-4">
      <Link href="/weekly-jobs" className="inline-flex items-center gap-1 text-sm text-accent-ink underline-offset-2 hover:underline">
        <svg viewBox="0 0 24 24" className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M15 6l-6 6 6 6" />
        </svg>
        {t("title")}
      </Link>
      {posting.isLoading || !enabled ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : posting.data ? (
        <PostingDetail posting={posting.data} headingLevel="h1" />
      ) : (
        <EmptyState title={t("notFound.title")} body={t("notFound.description")} />
      )}
    </div>
  );
}
