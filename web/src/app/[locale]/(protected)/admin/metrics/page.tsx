"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { Card } from "@/components/dashboard/Card";
import { StatTile } from "@/components/dashboard/StatTile";

/** Rates can be null (no cohort old enough yet). "—" is the honest rendering; 0% is not. */
function rateOrDash(value: number | null, locale: string): string {
  return value === null ? "—" : formatRate(value, locale);
}

function formatDay(isoDate: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", timeZone: "UTC" }).format(
    new Date(`${isoDate}T00:00:00Z`),
  );
}

export default function AdminMetricsPage() {
  const t = useTranslations("adminMetrics");
  const locale = useLocale();

  const { data, isLoading, error } = useQuery({
    queryKey: ["admin", "metrics"],
    queryFn: () => adminApi.getMetrics(30),
    // A 403 is a settled answer, not a hiccup — retrying it just delays the message.
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 403) && failureCount < 2,
  });

  const { data: calibration } = useQuery({
    queryKey: ["admin", "autoApprovalCalibration"],
    queryFn: adminApi.getAutoApprovalCalibration,
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 403) && failureCount < 2,
  });

  const days = data ?? [];
  const latest = days[0];

  if (error instanceof ApiError && error.status === 403) {
    return (
      <div className="flex flex-col gap-6">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <Card className="flex flex-col gap-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1.5">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-[62ch] text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        {latest ? (
          // The job runs every 30 minutes, so "when was this taken" is a real question with a
          // useful answer — without it the reader cannot tell a fresh page from a stalled job.
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t("computedAt", {
              at: new Date(latest.computedAt).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" }),
            })}
          </p>
        ) : null}
      </div>

      {isLoading ? <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p> : null}

      {!isLoading && !latest ? (
        <Card className="flex flex-col gap-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">{t("emptyTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("emptyBody")}</p>
        </Card>
      ) : null}

      {latest ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile label={t("activationRate")} value={formatRate(latest.activationRate, locale)} tone="accent" />
            <StatTile
              label={t("activatedUsers")}
              value={`${formatCount(latest.activatedUsers, locale)} / ${formatCount(latest.totalUsers, locale)}`}
            />
            <StatTile label={t("weeklyActiveUsers")} value={formatCount(latest.weeklyActiveUsers, locale)} />
            <StatTile label={t("d30Retention")} value={rateOrDash(latest.d30RetentionRate, locale)} />
          </div>

          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <StatTile label={t("totalApplications")} value={formatCount(latest.totalApplications, locale)} />
            <StatTile label={t("applicationsTracked30d")} value={formatCount(latest.applicationsTrackedLast30Days, locale)} />
            <StatTile label={t("statusUpdates30d")} value={formatCount(latest.statusUpdatesLast30Days, locale)} />
            <StatTile label={t("uniqueCompanies")} value={formatCount(latest.uniqueCompanies, locale)} />
          </div>

          <Card className="flex flex-col gap-3">
            <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("historyTitle")}</h2>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[44rem] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    <th className="py-2 pr-4 font-medium">{t("colDay")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colUsers")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colActivation")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colWau")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colD7")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colD30")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colD90")}</th>
                    <th className="py-2 font-medium">{t("colApplications")}</th>
                  </tr>
                </thead>
                <tbody>
                  {days.map((day) => (
                    <tr key={day.snapshotDate} className="border-b border-gray-100 last:border-b-0 dark:border-gray-900">
                      <td className="py-2 pr-4 whitespace-nowrap text-gray-900 dark:text-gray-100">
                        {formatDay(day.snapshotDate, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(day.totalUsers, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatRate(day.activationRate, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(day.weeklyActiveUsers, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {rateOrDash(day.d7RetentionRate, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {rateOrDash(day.d30RetentionRate, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {rateOrDash(day.d90RetentionRate, locale)}
                      </td>
                      <td className="py-2 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(day.totalApplications, locale)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </Card>
        </>
      ) : null}

      {calibration ? (
        <Card className="flex flex-col gap-3">
          <div className="flex flex-col gap-1">
            <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("calibrationTitle")}</h2>
            <p className="max-w-[68ch] text-xs text-gray-500 dark:text-gray-400">
              {t("calibrationBody", {
                threshold: `${Math.round(calibration.currentThreshold * 100)}%`,
                state: calibration.autoApplyEnabled
                  ? t("calibrationOn")
                  : calibration.shadowModeEnabled
                    ? t("calibrationShadow")
                    : t("calibrationOff"),
                total: formatCount(calibration.qualifyingTotal, locale),
              })}
            </p>
          </div>

          {calibration.qualifyingTotal === 0 ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">{t("calibrationEmpty")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[42rem] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    <th className="py-2 pr-4 font-medium">{t("colBand")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colTotal")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colConfirmed")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colDismissed")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colReverted")}</th>
                    <th className="py-2 pr-4 font-medium">{t("colAgreement")}</th>
                    <th className="py-2 font-medium">{t("colRevertRate")}</th>
                  </tr>
                </thead>
                <tbody>
                  {calibration.buckets.map((bucket) => (
                    <tr
                      key={bucket.lowerBound}
                      className="border-b border-gray-100 last:border-b-0 dark:border-gray-900"
                    >
                      <td className="py-2 pr-4 whitespace-nowrap tabular-nums text-gray-900 dark:text-gray-100">
                        {Math.round(bucket.lowerBound * 100)}–{Math.round(bucket.upperBound * 100)}%
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(bucket.total, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(bucket.confirmed, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(bucket.dismissed, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-600 dark:text-gray-400">
                        {formatCount(bucket.reverted, locale)}
                      </td>
                      <td className="py-2 pr-4 tabular-nums text-gray-500 dark:text-gray-500">
                        {rateOrDash(bucket.agreementRate, locale)}
                      </td>
                      {/* The one to read. Emphasised over the column left of it on purpose. */}
                      <td className="py-2 tabular-nums font-medium text-gray-900 dark:text-gray-100">
                        {rateOrDash(bucket.revertRate, locale)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      ) : null}
    </div>
  );
}
