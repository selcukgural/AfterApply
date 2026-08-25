"use client";

import { useTranslations } from "next-intl";
import type { ResponseTimeStatsResponse } from "@/types/api";

export function ResponseTimeCard({ stats }: { stats: ResponseTimeStatsResponse }) {
  const t = useTranslations("dashboard.responseTime");
  const hasData = stats.averageDays !== null && stats.medianDays !== null;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm dark:border-gray-800 dark:bg-gray-900">
      <p className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</p>
      {hasData ? (
        <div className="flex gap-6">
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("average")}</p>
            <p className="text-xl font-semibold text-gray-900 dark:text-gray-100">
              {stats.averageDays!.toFixed(1)} {t("days")}
            </p>
          </div>
          <div>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("median")}</p>
            <p className="text-xl font-semibold text-gray-900 dark:text-gray-100">
              {stats.medianDays!.toFixed(1)} {t("days")}
            </p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      )}
    </div>
  );
}
