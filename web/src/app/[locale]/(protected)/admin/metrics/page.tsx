"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { Card } from "@/components/dashboard/Card";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { summariseTraffic } from "@/lib/admin/siteTrafficSummary";
import { sevenDayChange, trendSeries } from "@/lib/admin/metricTrends";
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

/** A funnel bar's width: the step against all views, never narrower than a visible sliver when
 * the step has any count at all — at this traffic most steps are under 1% of views. */
function barWidth(value: number, whole: number): string {
  if (value <= 0 || whole <= 0) {
    return "0";
  }
  return `max(3px, ${Math.min(100, (value / whole) * 100)}%)`;
}

function RankTable({
  title,
  keyHeader,
  valueHeader,
  rows,
}: {
  title: string;
  keyHeader: string;
  valueHeader: string;
  rows: { key: string; label: string; value: string }[];
}) {
  return (
    <section className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <h2 className="border-b border-gray-100 px-5 py-3 text-sm font-semibold text-gray-900 dark:border-gray-800 dark:text-gray-100">
        {title}
      </h2>
      <table className="w-full text-left text-sm">
        <thead className="text-xs text-gray-500 dark:text-gray-400">
          <tr className="border-b border-gray-100 dark:border-gray-800">
            <th className="px-5 py-2 font-medium">{keyHeader}</th>
            <th className="px-5 py-2 text-right font-medium">{valueHeader}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr key={row.key} className="border-b border-gray-100 last:border-b-0 dark:border-gray-800">
              <td className="px-5 py-2 break-all text-gray-900 dark:text-gray-100">{row.label}</td>
              <td className="px-5 py-2 text-right tabular-nums text-gray-600 dark:text-gray-400">{row.value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
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
  const [showCalibration, setShowCalibration] = useState(false);
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

  const changeOf = (key: string) => {
    const trend = trends.find((item) => item.key === key)!;
    const change = sevenDayChange(trend.series);
    return change === null ? null : trend.formatChange(change);
  };
  const productCells = latest
    ? [
        { key: "activation", label: t("activationRate"), value: rate(latest.activationRate), change: changeOf("activation"), accent: true },
        {
          key: "users",
          label: t("activatedUsers"),
          value: `${count(latest.activatedUsers)} / ${count(latest.totalUsers)}`,
          change: changeOf("users"),
        },
        { key: "wau", label: t("weeklyActiveUsers"), value: count(latest.weeklyActiveUsers), change: changeOf("wau") },
        { key: "d30", label: t("d30Retention"), value: rateOrDash(latest.d30RetentionRate, locale), change: changeOf("d30") },
        {
          key: "applications",
          label: t("totalApplications"),
          value: count(latest.totalApplications),
          change: changeOf("applications"),
        },
        { key: "tracked30d", label: t("applicationsTracked30d"), value: count(latest.applicationsTrackedLast30Days), change: null },
        { key: "status30d", label: t("statusUpdates30d"), value: count(latest.statusUpdatesLast30Days), change: null },
        { key: "companies", label: t("uniqueCompanies"), value: count(latest.uniqueCompanies), change: null },
      ].map((cell) => ({ accent: false, ...cell }))
    : [];

  // A step's rate against the one it follows — null where that comparison would mean nothing
  // (the sign-up page is reached from everywhere, not only from the buttons above it).
  const rateOf = (part: number, whole: number) => (whole === 0 ? null : rate((part / whole) * 100));
  const funnelRows = trafficSummary
    ? [
        { key: "views", label: t("trafficPageViews"), count: trafficSummary.pageViews, rate: null },
        {
          key: "landing",
          label: t("trafficLandingViews"),
          count: trafficSummary.landingViews,
          rate: rateOf(trafficSummary.landingViews, trafficSummary.pageViews),
        },
        {
          key: "signUpClicks",
          label: t("funnelSignUpClicks"),
          detail: t("funnelSignUpClicksDetail", {
            inPage: count(trafficSummary.ctaClicks),
            header: count(trafficSummary.headerCtaClicks),
            hero: count(trafficSummary.heroSocialClicks),
            tools: count(trafficSummary.toolSignUpClicks),
          }),
          count: trafficSummary.signUpClicks,
          rate: rateOf(trafficSummary.signUpClicks, trafficSummary.landingViews),
          indent: true,
        },
        { key: "registerViews", label: t("trafficRegisterViews"), count: trafficSummary.registerViews, rate: null },
        {
          key: "formSent",
          label: t("funnelFormSent"),
          detail: t("funnelFormSentDetail", { rejected: count(trafficSummary.passwordRejected) }),
          count: trafficSummary.registerStarted,
          rate: rateOf(trafficSummary.registerStarted, trafficSummary.registerViews),
          indent: true,
        },
        { key: "socialStarted", label: t("funnelSocialStarted"), count: trafficSummary.socialStarted, rate: null, indent: true },
        {
          key: "newAccounts",
          label: t("funnelNewAccounts"),
          detail: t("funnelNewAccountsDetail", {
            form: count(trafficSummary.registerCompleted),
            social: count(trafficSummary.socialCompleted),
          }),
          count: trafficSummary.newAccounts,
          rate:
            trafficSummary.landingToRegister === null
              ? null
              : t("funnelNewAccountsRate", { rate: rate(trafficSummary.landingToRegister) }),
          total: true,
        },
      ].map((row) => ({ detail: undefined as string | undefined, indent: false, total: false, ...row }))
    : [];

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
          {/* One card, eight cells split by hairlines — not eight boxes. The first row is how the
              product is used, the second what it holds; a cell shows its 7-day change when the
              trend below has one. */}
          <section className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
            <div className="flex items-baseline justify-between gap-3 border-b border-gray-100 px-5 py-3 dark:border-gray-800">
              <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("productTitle")}</h2>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("productChangeHint")}</span>
            </div>
            {/* The hairlines are the grid's 1px gap showing its background, so no cell draws a
                border of its own and none is left dangling at the right edge or the bottom row. */}
            <dl className="grid grid-cols-2 gap-px bg-gray-100 sm:grid-cols-4 dark:bg-gray-800">
              {productCells.map((cell) => (
                <div key={cell.key} className="flex flex-col gap-1 bg-white px-5 py-4 dark:bg-gray-900">
                  <dt className="text-xs text-gray-600 dark:text-gray-400">{cell.label}</dt>
                  <dd
                    className={`text-2xl font-semibold tracking-tight tabular-nums ${
                      cell.accent ? "text-blue-700 dark:text-blue-400" : "text-gray-900 dark:text-gray-100"
                    }`}
                  >
                    {cell.value}
                  </dd>
                  {cell.change ? (
                    <dd className="text-xs text-gray-500 tabular-nums dark:text-gray-400">{cell.change}</dd>
                  ) : null}
                </div>
              ))}
            </dl>
          </section>

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

      {!trafficSummary ? (
        <Card className="flex flex-col gap-1">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("funnelTitle")}</h2>
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("trafficEmpty")}</p>
        </Card>
      ) : (
        <>
          {/* The sign-up funnel as rows, in the order a visitor meets the steps. Indented rows are
              the parts of the step above them. The bar is the step against all views; the rate
              column is the step against the one it follows, where that comparison means
              something. Both are ratios of independent totals — nobody was followed. */}
          <section className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
            <div className="flex flex-wrap items-start justify-between gap-3 border-b border-gray-100 px-5 py-3 dark:border-gray-800">
              <div className="flex flex-col gap-1">
                <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("funnelTitle")}</h2>
                <p className="max-w-[68ch] text-xs text-gray-500 dark:text-gray-400">{t("trafficSubtitle")}</p>
              </div>
              <div className="text-right">
                <p className="text-xs text-gray-600 dark:text-gray-400">{t("funnelNewAccounts")}</p>
                <p className="text-2xl font-semibold tabular-nums text-blue-700 dark:text-blue-400">
                  {count(trafficSummary.newAccounts)}
                </p>
              </div>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[36rem] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-gray-100 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    <th className="px-5 py-2 font-medium">{t("funnelColStep")}</th>
                    <th className="px-3 py-2 text-right font-medium">{t("funnelColCount")}</th>
                    <th className="w-1/3 px-3 py-2 font-medium">
                      <span className="sr-only">{t("funnelColBar")}</span>
                    </th>
                    <th className="px-5 py-2 text-right font-medium">{t("funnelColRate")}</th>
                  </tr>
                </thead>
                <tbody>
                  {funnelRows.map((step) => (
                    <tr
                      key={step.key}
                      className={`border-b border-gray-100 last:border-b-0 dark:border-gray-800 ${
                        step.total ? "bg-blue-50/60 dark:bg-blue-950/30" : ""
                      }`}
                    >
                      <td className={`py-2.5 pr-3 ${step.indent ? "pl-9" : "pl-5"}`}>
                        <span
                          className={
                            step.total
                              ? "font-semibold text-gray-900 dark:text-gray-100"
                              : step.indent
                                ? "text-gray-600 dark:text-gray-400"
                                : "text-gray-900 dark:text-gray-100"
                          }
                        >
                          {step.label}
                        </span>
                        {step.detail ? (
                          <span className="block text-xs text-gray-500 dark:text-gray-400">{step.detail}</span>
                        ) : null}
                      </td>
                      <td
                        className={`px-3 py-2.5 text-right tabular-nums ${
                          step.total ? "font-semibold text-gray-900 dark:text-gray-100" : "text-gray-700 dark:text-gray-300"
                        }`}
                      >
                        {count(step.count)}
                      </td>
                      <td className="px-3 py-2.5">
                        <div aria-hidden className="h-2 w-full rounded bg-gray-100 dark:bg-gray-800">
                          <div
                            className={`h-2 rounded ${step.total ? "bg-blue-700 dark:bg-blue-400" : "bg-blue-300 dark:bg-blue-700"}`}
                            style={{ width: barWidth(step.count, trafficSummary.pageViews) }}
                          />
                        </div>
                      </td>
                      <td
                        className={`px-5 py-2.5 text-right tabular-nums ${
                          step.total ? "font-medium text-gray-900 dark:text-gray-100" : "text-gray-600 dark:text-gray-400"
                        }`}
                      >
                        {step.rate ?? "—"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* The free tools side by side: the same three steps for each, so "people use it" and
              "people who use it reach for an account" can be told apart per tool. */}
          <section className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
            <div className="flex flex-col gap-1 border-b border-gray-100 px-5 py-3 dark:border-gray-800">
              <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("toolsTitle")}</h2>
              <p className="max-w-[68ch] text-xs text-gray-500 dark:text-gray-400">{t("toolsSubtitle")}</p>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[40rem] border-collapse text-sm">
                <thead>
                  <tr className="border-b border-gray-100 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    <th className="px-5 py-2 font-medium">{t("toolsColTool")}</th>
                    <th className="px-3 py-2 text-right font-medium">{t("toolsColViews")}</th>
                    <th className="px-3 py-2 text-right font-medium">{t("toolsColResults")}</th>
                    <th className="px-3 py-2 text-right font-medium">{t("toolsColSignUp")}</th>
                    <th className="px-5 py-2 text-right font-medium">{t("toolsColShares")}</th>
                  </tr>
                </thead>
                <tbody>
                  {trafficSummary.tools.map((tool) => (
                    <tr key={tool.key} className="border-b border-gray-100 last:border-b-0 dark:border-gray-800">
                      <td className="px-5 py-2.5">
                        <span className="text-gray-900 dark:text-gray-100">{t(`tools.${tool.key}.name`)}</span>
                        <span className="block text-xs text-gray-500 dark:text-gray-400">{t(`tools.${tool.key}.note`)}</span>
                      </td>
                      <td className="px-3 py-2.5 text-right tabular-nums text-gray-700 dark:text-gray-300">
                        {count(tool.views)}
                      </td>
                      <td className="px-3 py-2.5 text-right tabular-nums text-gray-700 dark:text-gray-300">
                        {tool.results === null ? (
                          "—"
                        ) : (
                          <>
                            {count(tool.results)}
                            {tool.views > 0 ? (
                              <span className="text-gray-500 dark:text-gray-400">
                                {" · "}
                                {rate((tool.results / tool.views) * 100)}
                              </span>
                            ) : null}
                          </>
                        )}
                      </td>
                      <td className="px-3 py-2.5 text-right tabular-nums text-gray-700 dark:text-gray-300">
                        {count(tool.signUpClicks)}
                      </td>
                      <td className="px-5 py-2.5 text-right tabular-nums text-gray-700 dark:text-gray-300">
                        {tool.shares === null ? "—" : count(tool.shares)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          <div className="grid gap-5 md:grid-cols-2">
            <RankTable
              title={t("trafficTopPages")}
              keyHeader={t("trafficColPage")}
              valueHeader={t("trafficColViews")}
              rows={trafficSummary.topPages.map(([path, views]) => ({ key: path, label: path, value: count(views) }))}
            />
            <RankTable
              title={t("trafficTopReferrers")}
              keyHeader={t("trafficColSource")}
              valueHeader={t("trafficColViews")}
              rows={trafficSummary.topReferrers.map(([host, views]) => ({
                key: host || "direct",
                // An empty host is a visit with no referrer — typed, bookmarked, or the referrer
                // was suppressed. Rendering it blank would read as a bug.
                label: host || t("trafficDirect"),
                value: count(views),
              }))}
            />
          </div>
        </>
      )}

      {calibration ? (
        // Last and closed by default: it has had no traffic yet, and an empty table at the top of the
        // page pushed the numbers that do move out of view.
        <Card className="flex flex-col gap-3">
          <div className="flex flex-wrap items-start justify-between gap-3">
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
            <Button
              variant="secondary"
              onClick={() => setShowCalibration((open) => !open)}
              aria-expanded={showCalibration}
            >
              {showCalibration ? t("calibrationHide") : t("calibrationShow")}
            </Button>
          </div>

          {!showCalibration ? null : calibration.qualifyingTotal === 0 ? (
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
                      <td className="py-2 pr-4 tabular-nums text-gray-500 dark:text-gray-450">
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
