"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Link } from "@/i18n/navigation";
import type { CompanyIntelligenceResponse, CompanyPublicResponse, ResponseRateFigures } from "@/types/api";
import { responseRatesApi } from "@/lib/api/responseRates";
import { formatDays, formatRate } from "@/lib/dashboard/format";
import { buttonClassName } from "@/components/ui/Button";

/** One query key for the tab strip and the panel: react-query answers the second reader from the
 *  first fetch, so the "Yanıt 64" count on the tab costs no extra request. */
export function companyIntelligenceQueryKey(companyId: string) {
  return ["company-intelligence", companyId] as const;
}

export function useCompanyIntelligence(companyId: string, enabled: boolean) {
  return useQuery({
    queryKey: companyIntelligenceQueryKey(companyId),
    queryFn: () => responseRatesApi.company(companyId),
    enabled,
    staleTime: 10 * 60 * 1000,
    retry: false,
  });
}

function formatWindowDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale, { day: "numeric", month: "short", year: "numeric" }).format(new Date(iso));
}

/** `value` is a 0–100 percentage or null; the dash is the honest render for "nobody reached that stage". */
function rateOrDash(value: number | null | undefined, locale: string, none: string): string {
  return value == null ? none : formatRate(value, locale);
}

const CARD = "rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900";

/**
 * The "Response" tab of a company page (design canvas 2026-09-21, direction A): three headline
 * figures with the sector median under each, a secondary row, the method card, and — below the
 * threshold — the three rules that keep the page closed instead of an empty panel. Public, like
 * the reviews; what guards the data is the threshold, not sign-in.
 */
export function CompanyIntelligencePanel({ company }: { company: CompanyPublicResponse }) {
  const t = useTranslations("companies.intelligence");
  const query = useCompanyIntelligence(company.id, true);

  if (query.isPending) {
    return <div className="h-40 animate-pulse rounded-xl bg-gray-100 dark:bg-gray-900" aria-hidden="true" />;
  }
  if (query.isError || !query.data) {
    return <p className="text-sm text-gray-600 dark:text-gray-400">{t("error")}</p>;
  }

  const data = query.data;
  return data.metrics ? <Figures data={data} /> : <BelowThreshold data={data} company={company} />;
}

function Figures({ data }: { data: CompanyIntelligenceResponse }) {
  const t = useTranslations("companies.intelligence");
  const tSectors = useTranslations("benchmark.sectors");
  const locale = useLocale();
  const metrics = data.metrics!;
  const sector = data.sectorComparison;
  const sectorName = sector ? tSectors(sector.sector) : null;
  const median = sector?.figures ?? null;
  const none = t("metrics.none");

  const sectorLine = (value: string | null): ReactNode =>
    value === null || sectorName === null ? (
      <span className="text-xs text-gray-500 dark:text-gray-400">{t("sectorUnavailable")}</span>
    ) : (
      <span className="text-xs text-gray-500 dark:text-gray-400">
        {t.rich("sectorMedian", {
          sector: sectorName,
          value,
          b: (chunks) => <b className="font-medium text-gray-700 dark:text-gray-300">{chunks}</b>,
        })}
      </span>
    );

  const secondary: { label: string; value: string; hint: string }[] = [
    {
      label: t("metrics.ghosting"),
      value: formatRate(metrics.ghostingRate, locale),
      hint: median ? t("sectorMedianShort", { value: formatRate(median.ghostingRate, locale) }) : "",
    },
    {
      label: t("metrics.interview"),
      value: formatRate(metrics.interviewRate, locale),
      hint: median ? t("sectorMedianShort", { value: formatRate(median.interviewRate, locale) }) : "",
    },
    {
      label: t("metrics.offer"),
      value: formatRate(metrics.offerRate, locale),
      hint: median ? t("sectorMedianShort", { value: formatRate(median.offerRate, locale) }) : "",
    },
    { label: t("metrics.closure"), value: formatRate(metrics.closureRate, locale), hint: t("metrics.closureHint") },
    // The two self-reported rates. Each has its own floor, so a dash here beside open figures is
    // "not enough answers yet", which the hint says rather than letting it read as a zero.
    {
      label: t("metrics.promiseKept"),
      value: rateOrDash(metrics.promiseKeptRate, locale, none),
      hint: metrics.promiseKeptRate == null
        ? t("metrics.subRateFloor")
        : median?.promiseKeptRate != null
          ? t("sectorMedianShort", { value: formatRate(median.promiseKeptRate, locale) })
          : t("metrics.promiseKeptHint"),
    },
    {
      label: t("metrics.rejectionNotice"),
      value: rateOrDash(metrics.rejectionNoticeRate, locale, none),
      hint: metrics.rejectionNoticeRate == null
        ? t("metrics.subRateFloor")
        : median?.rejectionNoticeRate != null
          ? t("sectorMedianShort", { value: formatRate(median.rejectionNoticeRate, locale) })
          : t("metrics.rejectionNoticeHint"),
    },
  ];

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t.rich("sampleLine", {
            start: formatWindowDate(data.windowStart, locale),
            end: formatWindowDate(data.windowEnd, locale),
            applications: metrics.totalApplications,
            contributors: metrics.distinctContributors,
            b: (chunks) => <b className="font-semibold text-gray-900 dark:text-gray-100">{chunks}</b>,
          })}
        </p>
        <ConfidenceBadge data={data} />
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <div className={`${CARD} flex flex-col gap-1.5`}>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("metrics.responseRate")}</span>
          <span className="text-3xl font-semibold tracking-tight text-good-ink tabular-nums">
            {formatRate(metrics.responseRate, locale)}
          </span>
          {sectorLine(median ? formatRate(median.responseRate, locale) : null)}
        </div>
        <div className={`${CARD} flex flex-col gap-1.5`}>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("metrics.medianFirstReply")}</span>
          <span className="text-3xl font-semibold tracking-tight text-gray-900 tabular-nums dark:text-gray-100">
            {metrics.medianResponseTimeDays === null ? none : formatDays(metrics.medianResponseTimeDays, locale)}{" "}
            {metrics.medianResponseTimeDays !== null && (
              <span className="text-base font-medium text-gray-500 dark:text-gray-400">{t("metrics.days")}</span>
            )}
          </span>
          {sectorLine(
            median?.medianFirstReplyDays == null ? null : `${formatDays(median.medianFirstReplyDays, locale)} ${t("metrics.days")}`,
          )}
        </div>
        <div className={`${CARD} flex flex-col gap-1.5`}>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("metrics.postInterviewSilence")}</span>
          <span className="text-3xl font-semibold tracking-tight text-warn-ink tabular-nums">
            {rateOrDash(metrics.postInterviewSilenceRate, locale, none)}
          </span>
          {sectorLine(median ? rateOrDash(median.postInterviewSilenceRate, locale, none) : null)}
        </div>
      </div>

      <div className={`${CARD} grid grid-cols-2 gap-3 sm:grid-cols-3`}>
        {secondary.map((item) => (
          <div key={item.label} className="flex flex-col gap-0.5">
            <span className="text-xs text-gray-500 dark:text-gray-400">{item.label}</span>
            <span className="text-lg font-semibold text-gray-900 tabular-nums dark:text-gray-100">{item.value}</span>
            {item.hint && <span className="text-[11px] text-gray-500 dark:text-gray-500">{item.hint}</span>}
          </div>
        ))}
      </div>

      <SilenceReportsCard data={data} />
      <MethodCard data={data} />
      <FooterLinks withHint />
    </div>
  );
}

function ConfidenceBadge({ data }: { data: CompanyIntelligenceResponse }) {
  const t = useTranslations("companies.intelligence");
  if (data.confidence === "Hidden") return null;
  // The ladder is the server's (CompanyIntelligenceOptions); the badge names the bucket the count
  // fell in, and the count itself is on the line beside it.
  return (
    <span className="inline-flex shrink-0 items-center rounded-full border border-warn/40 bg-warn-wash px-2.5 py-0.5 text-xs text-warn-ink">
      {t(`confidence.${data.confidence}`)}
    </span>
  );
}

function MethodCard({ data }: { data: CompanyIntelligenceResponse }) {
  const t = useTranslations("companies.intelligence");
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-4 text-xs leading-relaxed text-gray-600 dark:border-gray-800 dark:bg-gray-950 dark:text-gray-400">
      <p className="font-semibold text-gray-800 dark:text-gray-300">{t("method.title")}</p>
      <p>{t("method.source", { share: data.thresholds.maxContributorSharePercent })}</p>
      <p>
        {t("method.maturity", { days: data.thresholds.maturityDays, floor: data.thresholds.hiddenBelow })}{" "}
        <Link href="/response-rates#method" className="text-accent-ink underline-offset-2 hover:underline">
          {t("method.link")}
        </Link>
      </p>
    </div>
  );
}

function FooterLinks({ withHint }: { withHint?: boolean }) {
  const t = useTranslations("companies.intelligence");
  return (
    <div className="flex flex-col gap-2 text-sm sm:flex-row sm:items-center sm:justify-between">
      <Link href="/response-rates" className="text-accent-ink underline-offset-2 hover:underline">
        {t("footer.sectors")}
      </Link>
      <span className="text-gray-500 dark:text-gray-400">
        {t("footer.prompt")}{" "}
        <Link href="/applications/new" className="text-accent-ink underline-offset-2 hover:underline">
          {t("footer.add")}
        </Link>
        {withHint && t("footer.addHint")}
      </span>
    </div>
  );
}

function BelowThreshold({ data, company }: { data: CompanyIntelligenceResponse; company: CompanyPublicResponse }) {
  const t = useTranslations("companies.intelligence");
  const tSectors = useTranslations("benchmark.sectors");
  const locale = useLocale();
  const sector = data.sectorComparison;
  const rules: { label: string; value: string }[] = [
    { label: t("hidden.threshold"), value: t("hidden.thresholdValue", { floor: data.thresholds.hiddenBelow }) },
    { label: t("hidden.diversity"), value: t("hidden.diversityValue", { share: data.thresholds.maxContributorSharePercent }) },
    { label: t("hidden.maturity"), value: t("hidden.maturityValue", { days: data.thresholds.maturityDays }) },
  ];

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-4 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-6 dark:border-gray-700 dark:bg-gray-950">
        <div className="flex flex-col gap-1.5">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("hidden.title", { company: company.name })}</h2>
          <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {t("hidden.body", {
              start: formatWindowDate(data.windowStart, locale),
              end: formatWindowDate(data.windowEnd, locale),
            })}
          </p>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          {rules.map((rule) => (
            <div key={rule.label} className="flex flex-col gap-1 rounded-lg border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900">
              <span className="text-xs text-gray-500 dark:text-gray-400">{rule.label}</span>
              <span className="text-sm font-medium text-gray-900 dark:text-gray-100">{rule.value}</span>
            </div>
          ))}
        </div>
        <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-3">
          <Link href="/applications/new" className={buttonClassName("primary", "inline-flex w-fit")}>
            {t("hidden.cta")}
          </Link>
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("hidden.ctaHint")}</span>
        </div>
      </div>

      {sector?.figures && <SectorStrip sectorName={tSectors(sector.sector)} figures={sector.figures} />}
      <SilenceReportsCard data={data} />
    </div>
  );
}

/**
 * The anonymous "no reply" reports (growth item 1.6), on their own floor and apart from the
 * tracker's figures: only people who heard nothing fill the form in, so it is a count, never a
 * rate, and the card says so. Nothing renders below the floor — not even that reports exist.
 */
function SilenceReportsCard({ data }: { data: CompanyIntelligenceResponse }) {
  const t = useTranslations("companies.intelligence.silenceReports");
  const tStages = useTranslations("companies.silenceReport.stages");
  const reports = data.silenceReports;
  const thresholds = data.silenceReportThresholds;
  if (!reports || !thresholds) return null;

  const top = [...reports.byStage].sort((a, b) => b.count - a.count)[0];
  return (
    <div className={`${CARD} flex flex-col gap-1.5`}>
      <span className="text-xs text-gray-500 dark:text-gray-400">{t("label", { months: thresholds.windowMonths })}</span>
      <span className="text-2xl font-semibold tracking-tight text-gray-900 tabular-nums dark:text-gray-100">
        {t("count", { count: reports.count })}
      </span>
      {top && <span className="text-xs text-gray-600 dark:text-gray-400">{t("topStage", { stage: tStages(top.stage) })}</span>}
      <p className="text-xs leading-relaxed text-gray-500 dark:text-gray-400">
        {t("note", { reports: thresholds.minimumReports, quarters: thresholds.minimumQuarters })}
      </p>
    </div>
  );
}

function SectorStrip({ sectorName, figures }: { sectorName: string; figures: ResponseRateFigures }) {
  const t = useTranslations("companies.intelligence");
  const locale = useLocale();
  const none = t("metrics.none");
  const items: { value: string; label: string }[] = [
    { value: formatRate(figures.responseRate, locale), label: t("metrics.responseRate") },
    {
      value: figures.medianFirstReplyDays == null ? none : `${formatDays(figures.medianFirstReplyDays, locale)} ${t("metrics.days")}`,
      label: t("metrics.medianFirstReply"),
    },
    { value: rateOrDash(figures.postInterviewSilenceRate, locale, none), label: t("metrics.postInterviewSilence") },
  ];
  return (
    <div className={`${CARD} flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between`}>
      <div className="flex flex-col gap-0.5">
        <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("sectorStrip.title", { sector: sectorName })}</p>
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("sectorStrip.subtitle")}</p>
      </div>
      <div className="flex gap-5">
        {items.map((item) => (
          <span key={item.label} className="flex flex-col gap-0.5">
            <b className="text-lg font-semibold text-gray-900 tabular-nums dark:text-gray-100">{item.value}</b>
            <span className="text-[11px] text-gray-500 dark:text-gray-400">{item.label}</span>
          </span>
        ))}
      </div>
      <Link href="/response-rates" className="text-sm text-accent-ink underline-offset-2 hover:underline">
        {t("sectorStrip.link")}
      </Link>
    </div>
  );
}
