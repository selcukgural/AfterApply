"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { LandingIcon } from "@/components/landing/landingIcons";
import { useClientConfig } from "@/hooks/useClientConfig";

/**
 * The feature grid's card for the paid weekly postings. A client component inside a server
 * section because the feature ships dark: while `/api/config` says the routes do not exist, the
 * landing page must not advertise them, and the flag is only known on the client. Renders the
 * same card shell as its server-rendered neighbours, full width and last, with the "New" badge
 * the company pages had when they were the newest thing here.
 */
export function WeeklyJobsFeatureCard() {
  const t = useTranslations("landing.features");
  const { config } = useClientConfig();
  if (!config.jobSources?.enabled) {
    return null;
  }

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 sm:col-span-2 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-center gap-3">
        <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400">
          <LandingIcon name="weeklyJobs" />
        </span>
        <span className="ml-auto rounded-full bg-accent-wash px-2 py-0.5 text-xs text-accent-ink">{t("weeklyJobsBadge")}</span>
      </div>
      <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("weeklyJobsTitle")}</h3>
      <p className="text-sm text-gray-600 dark:text-gray-400">{t("weeklyJobsBody")}</p>
      <Link
        href="/help/weekly-jobs"
        className="mt-auto inline-flex w-fit items-center gap-1 pt-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400"
      >
        {t("weeklyJobsCta")}
        <span aria-hidden="true">→</span>
      </Link>
    </div>
  );
}
