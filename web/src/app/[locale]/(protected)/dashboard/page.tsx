"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { analyticsApi } from "@/lib/api/analytics";
import { applicationsApi } from "@/lib/api/applications";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { ConversionFunnel } from "@/components/dashboard/ConversionFunnel";
import { DashboardEmptyState } from "@/components/dashboard/DashboardEmptyState";
import { DashboardSkeleton } from "@/components/dashboard/DashboardSkeleton";
import { HeroTile } from "@/components/dashboard/HeroTile";
import { OutcomeCard } from "@/components/dashboard/OutcomeCard";
import { ResponseTimeCard } from "@/components/dashboard/ResponseTimeCard";
import { StatTile } from "@/components/dashboard/StatTile";
import { StatusBreakdown } from "@/components/dashboard/StatusBreakdown";
import { buttonClassName } from "@/components/ui/Button";

export default function DashboardPage() {
  const t = useTranslations("dashboard");
  const locale = useLocale();

  const { data: summary, isLoading: summaryLoading } = useQuery({
    queryKey: ["applications", "summary"],
    queryFn: applicationsApi.getSummary,
  });

  const { data: overview, isLoading: overviewLoading } = useQuery({
    queryKey: ["analytics", "overview"],
    queryFn: analyticsApi.getOverview,
  });

  const isLoading = summaryLoading || overviewLoading || !summary || !overview;

  // During a rolling deploy the web app can briefly talk to an API instance that predates
  // applicationsPerWeek. Missing trend data drops the sparkline; it must not take the page down.
  const trend = overview?.applicationsPerWeek?.map((week) => week.count) ?? [];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1.5">
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
          {!isLoading && summary.total > 0 ? (
            <p className="max-w-[62ch] text-sm text-gray-600 dark:text-gray-400">
              {t("headline", {
                total: formatCount(overview.rates.totalApplications, locale),
                responded: formatCount(overview.rates.respondedCount, locale),
                rate: formatRate(overview.rates.responseRate, locale),
                interviews: summary.interviews,
                offers: summary.offers,
              })}
            </p>
          ) : null}
        </div>
        <Link href="/applications/new" className={buttonClassName("primary")}>
          {t("newApplication")}
        </Link>
      </div>

      {isLoading ? (
        <DashboardSkeleton />
      ) : summary.total === 0 ? (
        <DashboardEmptyState />
      ) : (
        <div className="flex flex-col gap-4">
          {/*
            Every row is the same two-column split with the same gap, so one uninterrupted vertical
            gutter runs down the whole board. The earlier version sized each row to its content
            (4 columns, then 1.45fr/1fr, then 2fr/1fr) — three unrelated split points at 50%/75%,
            59% and 67%, which is what made the board read as misaligned.

            The four secondary tiles are a nested 2x2 inside the right half; their inner gutter
            lands exactly on the 75% line, because the nested gap matches the outer one.
          */}
          <div className="grid gap-4 lg:grid-cols-2">
            <div>
              <HeroTile
                label={t("hero.label")}
                value={formatCount(summary.active, locale)}
                sub={t("hero.sub", {
                  total: formatCount(summary.total, locale),
                  share: formatRate(
                    summary.total === 0 ? 0 : (100 * summary.active) / summary.total,
                    locale,
                  ),
                })}
                trend={trend}
                trendLabel={t("hero.trendLabel", { weeks: trend.length })}
                trendAriaLabel={t("hero.trendAria", { weeks: trend.length })}
              />
            </div>

            <div className="grid grid-cols-2 gap-4">
              <StatTile
                tone="accent"
                label={t("tiles.interviews")}
                value={formatCount(summary.interviews, locale)}
                chip={t("chips.rate", { rate: formatRate(overview.rates.interviewRate, locale) })}
              />
              <StatTile
                tone="good"
                label={t("tiles.offers")}
                value={formatCount(summary.offers, locale)}
                chip={t("chips.rate", { rate: formatRate(overview.rates.offerRate, locale) })}
              />
              <StatTile
                tone="crit"
                label={t("tiles.rejected")}
                value={formatCount(summary.rejected, locale)}
                chip={t("chips.rate", { rate: formatRate(overview.rates.rejectionRate, locale) })}
              />
              <StatTile
                tone="muted"
                label={t("tiles.ghosted")}
                value={formatCount(summary.ghosted, locale)}
                chip={t("chips.rate", { rate: formatRate(overview.rates.ghostingRate, locale) })}
              />
            </div>
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <ConversionFunnel rates={overview.rates} />
            <OutcomeCard distribution={overview.statusDistribution} />
          </div>

          <div className="grid gap-4 lg:grid-cols-2">
            <StatusBreakdown data={overview.statusDistribution} />
            <ResponseTimeCard stats={overview.responseTime} />
          </div>
        </div>
      )}
    </div>
  );
}
