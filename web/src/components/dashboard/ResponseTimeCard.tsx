"use client";

import { useLocale, useTranslations } from "next-intl";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { formatDays } from "@/lib/dashboard/format";
import type { ResponseTimeStatsResponse } from "@/types/api";

/** Beyond this the mean is being dragged by a few very late replies and says little on its own. */
const SKEW_FACTOR = 3;

export function ResponseTimeCard({ stats }: { stats: ResponseTimeStatsResponse }) {
  const t = useTranslations("dashboard.responseTime");
  const locale = useLocale();

  if (stats.averageDays === null || stats.medianDays === null) {
    return (
      <Card>
        <CardHeader title={t("title")} />
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      </Card>
    );
  }

  // Median leads: it is the robust figure, and on a mailbox full of same-day auto-rejections the
  // mean is the misleading one. A bare "0.5 days" is also meaningless without the sample it came
  // from, so sampleSize — already in the API, never shown — sits in the header.
  const skewed = stats.medianDays > 0 && stats.averageDays > SKEW_FACTOR * stats.medianDays;

  return (
    <Card className="flex flex-col">
      <CardHeader title={t("title")} hint={t("sample", { count: stats.sampleSize })} />

      {/* flex-1 + justify-center: this card holds two figures and is always the shortest in its
          row, so it centres them in whatever height the row settles on instead of stacking them
          at the top under a void. */}
      <div className="flex flex-1 flex-wrap content-center gap-x-8 gap-y-3">
        <div className="flex flex-col gap-0.5">
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("median")}</span>
          <span className="text-2xl font-semibold text-gray-900 dark:text-gray-100">
            {formatDays(stats.medianDays, locale)}
            <span className="ml-1 text-sm font-normal text-gray-500 dark:text-gray-400">{t("days")}</span>
          </span>
        </div>
        <div className="flex flex-col gap-0.5">
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("average")}</span>
          <span className="text-2xl font-semibold text-gray-900 dark:text-gray-100">
            {formatDays(stats.averageDays, locale)}
            <span className="ml-1 text-sm font-normal text-gray-500 dark:text-gray-400">{t("days")}</span>
          </span>
        </div>
      </div>

      {skewed ? (
        <p className="mt-auto border-t border-gray-100 pt-3 text-sm text-gray-600 dark:border-gray-800/60 dark:text-gray-400">
          {t("skewed")}
        </p>
      ) : null}
    </Card>
  );
}
