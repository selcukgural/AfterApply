"use client";

import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";

/** The "Today" list's line for the paid weekly postings — only once the feature is live. */
export function WeeklyJobsRoadmapItem() {
  const t = useTranslations("landing.roadmap");
  const { config } = useClientConfig();
  if (!config.jobSources?.enabled) {
    return null;
  }

  return (
    <li className="flex items-center gap-2">
      <span className="text-green-600 dark:text-green-400" aria-hidden="true">
        ✓
      </span>
      {t("todayWeeklyJobs")}
      <span className="rounded-full bg-accent-wash px-1.5 py-px text-[11px] font-medium leading-[14px] text-accent-ink">
        {t("proBadge")}
      </span>
    </li>
  );
}
