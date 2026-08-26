import { getTranslations } from "next-intl/server";
import type { ApplicationStatus } from "@/types/api";
import { StatTile } from "@/components/dashboard/StatTile";
import { ResponseTimeCard } from "@/components/dashboard/ResponseTimeCard";
import { StatusDistributionChart } from "@/components/dashboard/StatusDistributionChart";
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

export async function AnalyticsSection() {
  const t = await getTranslations("landing.analytics");
  const tCommon = await getTranslations("landing.common");
  const tRates = await getTranslations("dashboard.rates");

  const rateTiles = [
    { key: "responseRate", value: 63 },
    { key: "interviewRate", value: 18 },
    { key: "offerRate", value: 3 },
    { key: "rejectionRate", value: 39 },
    { key: "ghostingRate", value: 14 },
  ] as const;

  return (
    <section className="py-20">
      <ScrollReveal className="mx-auto flex max-w-5xl flex-col gap-10 px-4">
        <div className="flex flex-col gap-3 text-center">
          <div className="flex items-center justify-center gap-2">
            <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
              {tCommon("sampleData")}
            </span>
          </div>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        </div>

        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
          {rateTiles.map((tile) => (
            <StatTile key={tile.key} label={tRates(tile.key)} value={tile.value} suffix="%" />
          ))}
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <ResponseTimeCard stats={{ sampleSize: 80, averageDays: 6.4, medianDays: 4 }} />
          <StatusDistributionChart data={MOCK_STATUS_DISTRIBUTION} />
        </div>
      </ScrollReveal>
    </section>
  );
}
