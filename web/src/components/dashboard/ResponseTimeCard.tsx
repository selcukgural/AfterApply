"use client";

import { useTranslations } from "next-intl";
import type { ResponseTimeStatsResponse } from "@/types/api";

export function ResponseTimeCard({ stats }: { stats: ResponseTimeStatsResponse }) {
  const t = useTranslations("dashboard.responseTime");
  const hasData = stats.averageDays !== null && stats.medianDays !== null;

  return (
    <div className="rounded-lg border border-gray-200 bg-white p-4 shadow-sm">
      <p className="mb-2 text-sm font-medium text-gray-700">{t("title")}</p>
      {hasData ? (
        <div className="flex gap-6">
          <div>
            <p className="text-xs text-gray-500">{t("average")}</p>
            <p className="text-xl font-semibold text-gray-900">
              {stats.averageDays!.toFixed(1)} {t("days")}
            </p>
          </div>
          <div>
            <p className="text-xs text-gray-500">{t("median")}</p>
            <p className="text-xl font-semibold text-gray-900">
              {stats.medianDays!.toFixed(1)} {t("days")}
            </p>
          </div>
        </div>
      ) : (
        <p className="text-sm text-gray-500">{t("empty")}</p>
      )}
    </div>
  );
}
