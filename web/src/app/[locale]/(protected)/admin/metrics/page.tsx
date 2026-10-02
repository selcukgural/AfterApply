"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { Card } from "@/components/dashboard/Card";
import { StatTile } from "@/components/dashboard/StatTile";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { summariseTraffic } from "@/lib/admin/siteTrafficSummary";
import { trendSeries } from "@/lib/admin/metricTrends";
import { MetricTrendCard } from "@/components/admin/MetricTrendCard";
import { Button } from "@/components/ui/Button";

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

  const { data: traffic } = useQuery({
    queryKey: ["admin", "siteTraffic"],
    queryFn: () => adminApi.getSiteTraffic(30),
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 403) && failureCount < 2,
  });

  const [showTable, setShowTable] = useState(false);
  const days = data ?? [];
  const latest = days[0];
  const trafficSummary = summariseTraffic(traffic);

  const signed = (text: string, value: number) => (value > 0 ? `+${text}` : value < 0 ? `−${text.replace("-", "")}` : text);
  const countChange = (value: number) => signed(formatCount(Math.abs(value), locale), value);
  // Rates move in percentage points; "+2,1 pt" rather than a percentage of a percentage.
  const pointChange = (value: number) =>
    signed(
      `${new Intl.NumberFormat(locale, { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(Math.abs(value))} pt`,
      value,
    );
  const count = (value: number) => formatCount(value, locale);
  const rate = (value: number) => formatRate(value, locale);
  const trends = [
    { key: "users", label: t("colUsers"), series: trendSeries(days, (d) => d.totalUsers), formatValue: count, formatChange: countChange },
    { key: "activation", label: t("colActivation"), series: trendSeries(days, (d) => d.activationRate), formatValue: rate, formatChange: pointChange },
    { key: "wau", label: t("colWau"), series: trendSeries(days, (d) => d.weeklyActiveUsers), formatValue: count, formatChange: countChange },
    { key: "d7", label: t("colD7"), series: trendSeries(days, (d) => d.d7RetentionRate), formatValue: rate, formatChange: pointChange },
    { key: "d30", label: t("colD30"), series: trendSeries(days, (d) => d.d30RetentionRate), formatValue: rate, formatChange: pointChange },
    { key: "applications", label: t("colApplications"), series: trendSeries(days, (d) => d.totalApplications), formatValue: count, formatChange: countChange },
  ];

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
      <AdminTabs />

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

          <Card className="flex flex-col gap-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="flex flex-col gap-1">
                <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("historyTitle")}</h2>
                <p className="max-w-[68ch] text-xs text-gray-500 dark:text-gray-400">{t("trendHint")}</p>
              </div>
              <Button variant="secondary" onClick={() => setShowTable((open) => !open)} aria-expanded={showTable}>
                {showTable ? t("hideTable") : t("showTable")}
              </Button>
            </div>

            {/* One card per metric the table had, minus 90. gün: its cohort does not exist yet,
                so it would be an empty card; the table keeps the column. */}
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
              {trends.map((trend) => (
                <MetricTrendCard
                  key={trend.key}
                  label={trend.label}
                  series={trend.series}
                  formatValue={trend.formatValue}
                  formatChange={trend.formatChange}
                  formatDay={(isoDate) => formatDay(isoDate, locale)}
                  changeLabel={(change) => t("trendChange", { change })}
                  emptyLabel={t("trendNoComparison")}
                />
              ))}
            </div>

            {showTable ? (
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
            ) : null}
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

      <Card className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h2 className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("trafficTitle")}</h2>
          <p className="max-w-[68ch] text-xs text-gray-500 dark:text-gray-400">{t("trafficSubtitle")}</p>
        </div>

        {!trafficSummary ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("trafficEmpty")}</p>
        ) : (
          <>
            {/* Two rows, in the order a visitor meets them: arriving and reaching for the sign-up,
                then the two ways of finishing it. The social row is most of the accounts. */}
            <div className="flex flex-col gap-2">
              <h3 className="text-xs font-medium text-gray-600 dark:text-gray-400">{t("trafficArrivalTitle")}</h3>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 lg:grid-cols-7">
                <StatTile label={t("trafficPageViews")} value={formatCount(trafficSummary.pageViews, locale)} />
                <StatTile label={t("trafficLandingViews")} value={formatCount(trafficSummary.landingViews, locale)} />
                <StatTile label={t("trafficCvScans")} value={formatCount(trafficSummary.cvScans, locale)} />
                <StatTile label={t("trafficCtaClicks")} value={formatCount(trafficSummary.ctaClicks, locale)} />
                <StatTile label={t("trafficHeaderCtaClicks")} value={formatCount(trafficSummary.headerCtaClicks, locale)} />
                <StatTile label={t("trafficHeroSocialClicks")} value={formatCount(trafficSummary.heroSocialClicks, locale)} />
                <StatTile label={t("trafficRegisterViews")} value={formatCount(trafficSummary.registerViews, locale)} />
              </div>
            </div>

            <div className="flex flex-col gap-2">
              <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
                <h3 className="text-xs font-medium text-gray-600 dark:text-gray-400">{t("trafficSignUpTitle")}</h3>
                <p className="text-xs text-gray-500 tabular-nums dark:text-gray-400">
                  {t("trafficLandingToRegister", { rate: rateOrDash(trafficSummary.landingToRegister, locale) })}
                </p>
              </div>
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
                <StatTile
                  label={t("trafficRegisterStarted")}
                  value={formatCount(trafficSummary.registerStarted, locale)}
                />
                <StatTile
                  label={t("trafficPasswordRejected")}
                  value={formatCount(trafficSummary.passwordRejected, locale)}
                />
                <StatTile
                  label={t("trafficRegisterCompleted")}
                  value={formatCount(trafficSummary.registerCompleted, locale)}
                />
                <StatTile label={t("trafficSocialStarted")} value={formatCount(trafficSummary.socialStarted, locale)} />
                <StatTile
                  label={t("trafficSocialCompleted")}
                  value={formatCount(trafficSummary.socialCompleted, locale)}
                />
                <StatTile
                  label={t("trafficNewAccounts")}
                  value={formatCount(trafficSummary.newAccounts, locale)}
                  tone="accent"
                />
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2">
              <div className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-gray-600 dark:text-gray-400">{t("trafficTopPages")}</h3>
                <table className="w-full text-left text-sm">
                  <thead className="text-xs text-gray-500 dark:text-gray-400">
                    <tr className="border-b border-gray-200 dark:border-gray-800">
                      <th className="py-2 pr-4 font-medium">{t("trafficColPage")}</th>
                      <th className="py-2 font-medium">{t("trafficColViews")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {trafficSummary.topPages.map(([path, count]) => (
                      <tr key={path} className="border-b border-gray-100 last:border-b-0 dark:border-gray-900">
                        <td className="py-2 pr-4 text-gray-900 dark:text-gray-100">{path}</td>
                        <td className="py-2 tabular-nums text-gray-600 dark:text-gray-400">
                          {formatCount(count, locale)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="flex flex-col gap-2">
                <h3 className="text-xs font-medium text-gray-600 dark:text-gray-400">{t("trafficTopReferrers")}</h3>
                <table className="w-full text-left text-sm">
                  <thead className="text-xs text-gray-500 dark:text-gray-400">
                    <tr className="border-b border-gray-200 dark:border-gray-800">
                      <th className="py-2 pr-4 font-medium">{t("trafficColSource")}</th>
                      <th className="py-2 font-medium">{t("trafficColViews")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {trafficSummary.topReferrers.map(([host, count]) => (
                      <tr key={host || "direct"} className="border-b border-gray-100 last:border-b-0 dark:border-gray-900">
                        {/* An empty host is a visit with no referrer — typed, bookmarked, or the
                            referrer was suppressed. Rendering it blank would read as a bug. */}
                        <td className="py-2 pr-4 text-gray-900 dark:text-gray-100">{host || t("trafficDirect")}</td>
                        <td className="py-2 tabular-nums text-gray-600 dark:text-gray-400">
                          {formatCount(count, locale)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          </>
        )}
      </Card>
    </div>
  );
}
