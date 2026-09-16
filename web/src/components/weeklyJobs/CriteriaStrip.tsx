"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { JobSourceProfileResponse } from "@/types/api";

/** The one-line summary of the saved criteria above the list, with the way to the form. */
export function CriteriaStrip({ profile }: { profile: JobSourceProfileResponse }) {
  const t = useTranslations("weeklyJobs.strip");
  const chip = "rounded-full bg-muted-wash px-2.5 py-0.5 text-xs text-muted-ink";
  return (
    <div className="flex items-center justify-between gap-4 rounded-lg border border-gray-200 bg-white px-4 py-3 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs text-gray-500 dark:text-gray-400">{t("searching")}</span>
        {profile.titles.map((title) => (
          <span key={title} className={chip}>
            {title}
          </span>
        ))}
        <span className={chip}>{profile.location}</span>
        {profile.remoteOnly && <span className={chip}>{t("remoteOnly")}</span>}
        {profile.minScore > 0 && (
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("minScore", { score: profile.minScore })}</span>
        )}
      </div>
      <Link href="/weekly-jobs/criteria" className="shrink-0 text-sm text-accent-ink underline-offset-2 hover:underline">
        {t("edit")}
      </Link>
    </div>
  );
}
