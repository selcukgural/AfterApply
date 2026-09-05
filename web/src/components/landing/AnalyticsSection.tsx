import { getLocale, getTranslations } from "next-intl/server";
import type { AnalyticsRatesResponse, ApplicationStatus } from "@/types/api";
import { formatRate } from "@/lib/dashboard/format";
import type { Tone } from "@/lib/dashboard/statusGroups";
import { ConversionFunnel } from "@/components/dashboard/ConversionFunnel";
import { OutcomeCard } from "@/components/dashboard/OutcomeCard";
import { ResponseTimeCard } from "@/components/dashboard/ResponseTimeCard";
import { StatTile } from "@/components/dashboard/StatTile";
import { StatusBreakdown } from "@/components/dashboard/StatusBreakdown";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

const MOCK_STATUS_DISTRIBUTION: { status: ApplicationStatus; count: number }[] = [
  { status: "Applied", count: 40 },
  { status: "Screening", count: 15 },
  { status: "Interview", count: 18 },
  { status: "TechnicalInterview", count: 10 },
  { status: "FinalInterview", count: 6 },
  { status: "Offer", count: 3 },
  { status: "Rejected", count: 25 },
  { status: "Ghosted", count: 10 },
];

// Shaped like a real /api/analytics/overview payload so the funnel below is the same component,
// with the same maths, that the signed-in dashboard runs.
const MOCK_RATES: AnalyticsRatesResponse = {
  totalApplications: 127,
  respondedCount: 80,
  responseRate: 63,
  interviewCount: 23,
  interviewRate: 18.1,
  offerCount: 4,
  offerRate: 3.1,
  rejectedCount: 49,
  rejectionRate: 38.6,
  ghostedCount: 18,
  ghostingRate: 14.2,
};

export async function AnalyticsSection() {
  const t = await getTranslations("landing.analytics");
  const tCommon = await getTranslations("landing.common");
  const tRates = await getTranslations("dashboard.rates");
  const locale = await getLocale();

  const rateTiles: { key: keyof AnalyticsRatesResponse; value: number; tone: Tone }[] = [
    { key: "responseRate", value: MOCK_RATES.responseRate, tone: "accent" },
    { key: "interviewRate", value: MOCK_RATES.interviewRate, tone: "accent" },
    { key: "offerRate", value: MOCK_RATES.offerRate, tone: "good" },
    { key: "rejectionRate", value: MOCK_RATES.rejectionRate, tone: "crit" },
    { key: "ghostingRate", value: MOCK_RATES.ghostingRate, tone: "muted" },
  ];

  return (
    <section className="py-20">
      <ScrollReveal className="mx-auto flex max-w-5xl flex-col gap-10 px-4">
        <div className="flex flex-col gap-3 text-center">
          <div className="flex items-center justify-center gap-2">
            <span className="text-sm font-medium text-accent-ink">{t("eyebrow")}</span>
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
              {tCommon("sampleData")}
            </span>
          </div>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        </div>

        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {rateTiles.map((tile) => (
            <StatTile
              key={tile.key}
              tone={tile.tone}
              label={tRates(tile.key)}
              value={formatRate(tile.value, locale)}
            />
          ))}
        </div>

        {/* The same single two-column split the signed-in dashboard uses, so both boards share
            one vertical gutter. */}
        <div className="grid gap-4 lg:grid-cols-2">
          <ConversionFunnel rates={MOCK_RATES} />
          <OutcomeCard distribution={MOCK_STATUS_DISTRIBUTION} />
        </div>

        <div className="grid gap-4 lg:grid-cols-2">
          <StatusBreakdown data={MOCK_STATUS_DISTRIBUTION} />
          <ResponseTimeCard stats={{ sampleSize: 80, averageDays: 6.4, medianDays: 4 }} />
        </div>
      </ScrollReveal>
    </section>
  );
}
