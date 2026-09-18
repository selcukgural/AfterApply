"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { analyticsApi } from "@/lib/api/analytics";
import { applicationsApi } from "@/lib/api/applications";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { progressKey, rateChip } from "@/lib/dashboard/tone";
import { ConversionFunnel } from "@/components/dashboard/ConversionFunnel";
import { DashboardEmptyState } from "@/components/dashboard/DashboardEmptyState";
import { DashboardSkeleton } from "@/components/dashboard/DashboardSkeleton";
import { HeroTile } from "@/components/dashboard/HeroTile";
import { OutcomeCard } from "@/components/dashboard/OutcomeCard";
import { RemindersPanel } from "@/components/dashboard/RemindersPanel";
import { WeeklyJobsAnnouncement } from "@/components/dashboard/WeeklyJobsAnnouncement";
import { StaleApplicationsBanner } from "@/components/dashboard/StaleApplicationsBanner";
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
              })}
              {/* The second sentence only names what is actually in motion. "0 offers awaiting a
                  decision" is not news to the person reading it — it is the one number they came
                  here hoping not to see, and the page has no business repeating it at them. */}
              {progressKey(summary.interviews, summary.offers) ? (
                <>
                  {" "}
                  {t(`headlineProgress.${progressKey(summary.interviews, summary.offers)}`, {
                    interviews: summary.interviews,
                    offers: summary.offers,
                  })}
                </>
              ) : null}
            </p>
          ) : null}
        </div>
        {/* Phones only: since 2026-09-17 the header carries this button on every page, and on a
            desktop two identical buttons forty pixels apart read as a mistake. On a phone the
            header's copy is inside the drawer, so the page keeps its own. */}
        <Link href="/applications/new" className={buttonClassName("primary", "md:hidden")}>
          {t("newApplication")}
        </Link>
      </div>

      {/* Above everything else, for the accounts it applies to: the one thing on this page that
          is news rather than the user's own numbers. */}
      <WeeklyJobsAnnouncement />

      {isLoading ? (
        <DashboardSkeleton />
      ) : summary.total === 0 ? (
        <DashboardEmptyState />
      ) : (
        <div className="flex flex-col gap-4">
          {/* Above the board, not beside it: these are the two things here that ask for an action.
              The stale question first — one answer covers an entire old import, and it is the
              reason the reminders card below stays short. */}
          <StaleApplicationsBanner />
          <RemindersPanel />
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
                chip={rateChip(summary.interviews, t("chips.rate", { rate: formatRate(overview.rates.interviewRate, locale) }))}
              />
              <StatTile
                tone="good"
                label={t("tiles.offers")}
                value={formatCount(summary.offers, locale)}
                chip={rateChip(summary.offers, t("chips.rate", { rate: formatRate(overview.rates.offerRate, locale) }))}
              />
              {/* Rejected is muted, not "crit": a rejection is an outcome, not an error, and red on
                  the tile that counts them turns a board of facts into a board of blame. Red on
                  this page is reserved for things that went wrong in the app (DEVELOPMENT_PLAN.md,
                  T-series). */}
              <StatTile
                tone="muted"
                label={t("tiles.rejected")}
                value={formatCount(summary.rejected, locale)}
                chip={rateChip(summary.rejected, t("chips.rate", { rate: formatRate(overview.rates.rejectionRate, locale) }))}
              />
              <StatTile
                tone="muted"
                label={t("tiles.ghosted")}
                value={formatCount(summary.ghosted, locale)}
                chip={rateChip(summary.ghosted, t("chips.rate", { rate: formatRate(overview.rates.ghostingRate, locale) }))}
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
