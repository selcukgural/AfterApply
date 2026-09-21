"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { BenchmarkPeriod, SectorResponseRateRow, SectorResponseRatesResponse } from "@/types/api";
import { responseRatesApi } from "@/lib/api/responseRates";
import { formatCount, formatDays, formatRate } from "@/lib/dashboard/format";
import { splitSectorRows } from "@/lib/responseRates/rows";

type Period = Exclude<BenchmarkPeriod, "Longer">;
const PERIODS: readonly Period[] = ["LastTwelveMonths", "LastSixMonths", "LastThreeMonths"];

/**
 * The public sector table (design canvas 2026-09-21, direction "tablo"): one row per sector,
 * figures for the ones above the threshold, a greyed reason for the ones below, and the
 * thresholds printed from the server's own answer so the text can never drift from the rule.
 * Fetched here rather than on the server so the period switch is a query, not a navigation;
 * the page shell around it (title, lead, method) is static and indexable.
 */
export function SectorResponseRatesTable() {
  const t = useTranslations("responseRates");
  const tPeriods = useTranslations("benchmark.periods");
  const [period, setPeriod] = useState<Period>("LastTwelveMonths");
  const query = useQuery({
    queryKey: ["response-rates", "sectors", period],
    queryFn: () => responseRatesApi.sectors(period),
    staleTime: 60 * 60 * 1000,
    retry: false,
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div role="group" aria-label={t("periodLabel")} className="inline-flex overflow-hidden rounded-md border border-gray-200 text-sm dark:border-gray-800">
          {PERIODS.map((item) => {
            const selected = item === period;
            return (
              <button
                key={item}
                type="button"
                aria-pressed={selected}
                onClick={() => setPeriod(item)}
                className={
                  selected
                    ? "bg-gray-200 px-3 py-1.5 text-gray-900 dark:bg-gray-800 dark:text-gray-100"
                    : "px-3 py-1.5 text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-900"
                }
              >
                {tPeriods(item)}
              </button>
            );
          })}
        </div>
        {query.data && <Counts data={query.data} />}
      </div>

      {query.isPending && <div className="h-64 animate-pulse rounded-xl bg-gray-100 dark:bg-gray-900" aria-hidden="true" />}
      {query.isError && <p className="text-sm text-gray-600 dark:text-gray-400">{t("error")}</p>}
      {query.data && <Table data={query.data} />}
    </div>
  );
}

function Counts({ data }: { data: SectorResponseRatesResponse }) {
  const t = useTranslations("responseRates");
  const locale = useLocale();
  const { visible, hidden } = splitSectorRows(data.sectors);
  const updated = new Intl.DateTimeFormat(locale, { day: "numeric", month: "long", year: "numeric" }).format(new Date(data.windowEnd));
  return (
    <p className="text-xs text-gray-500 dark:text-gray-500">
      {t("updated", { date: updated })} · {t("counts", { visible: visible.length, hidden: hidden.length })}
    </p>
  );
}

const HEAD = "px-4 py-2.5 text-[11px] font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400";
const CELL = "px-4 py-3 text-sm text-gray-900 dark:text-gray-100";
const NUM = `${CELL} text-right tabular-nums`;

function Table({ data }: { data: SectorResponseRatesResponse }) {
  const t = useTranslations("responseRates");
  const tSectors = useTranslations("benchmark.sectors");
  const locale = useLocale();
  const { visible, hidden } = splitSectorRows(data.sectors);
  const thresholds = data.thresholds;
  const none = t("none");

  const rateCell = (value: number | null) => (value === null ? none : formatRate(value, locale));

  if (visible.length === 0) {
    return (
      <div className="flex flex-col gap-2 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-6 dark:border-gray-700 dark:bg-gray-950">
        <p className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("empty.title")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("empty.body", { contributors: thresholds.minimumContributors, applications: thresholds.minimumApplications })}
        </p>
        <HiddenNote hidden={hidden} data={data} />
      </div>
    );
  }

  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] border-collapse">
          <thead className="border-b border-gray-200 dark:border-gray-800">
            <tr>
              <th scope="col" className={`${HEAD} text-left`}>{t("columns.sector")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.applications")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.contributors")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.responseRate")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.medianFirstReply")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.postInterviewSilence")}</th>
              <th scope="col" className={`${HEAD} text-right`}>{t("columns.ghosting")}</th>
            </tr>
          </thead>
          <tbody>
            {visible.map((row) => {
              const f = row.figures!;
              return (
                <tr key={row.sector} className="border-b border-gray-100 last:border-b-0 dark:border-gray-800">
                  <th scope="row" className={`${CELL} text-left font-medium`}>{tSectors(row.sector)}</th>
                  <td className={`${NUM} text-gray-500 dark:text-gray-400`}>{formatCount(f.applications, locale)}</td>
                  <td className={`${NUM} text-gray-500 dark:text-gray-400`}>{formatCount(f.contributors, locale)}</td>
                  <td className={`${NUM} font-semibold`}>{formatRate(f.responseRate, locale)}</td>
                  <td className={NUM}>{f.medianFirstReplyDays === null ? none : t("days", { value: formatDays(f.medianFirstReplyDays, locale) })}</td>
                  <td className={NUM}>{rateCell(f.postInterviewSilenceRate)}</td>
                  <td className={NUM}>{formatRate(f.ghostingRate, locale)}</td>
                </tr>
              );
            })}
            {hidden.map((row) => (
              <tr key={row.sector} className="border-b border-gray-100 text-gray-500 last:border-b-0 dark:border-gray-800 dark:text-gray-500">
                <th scope="row" className={`${CELL} text-left font-normal text-gray-500 dark:text-gray-500`}>{tSectors(row.sector)}</th>
                <td colSpan={6} className={`${CELL} text-right text-xs text-gray-500 dark:text-gray-500`}>
                  {t("belowThreshold", { contributors: thresholds.minimumContributors, applications: thresholds.minimumApplications })}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="flex flex-col gap-1 border-t border-gray-200 px-4 py-3 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-500">
        <span>{t("shareRule", { share: thresholds.maxContributorSharePercent })}</span>
        {data.unclassifiedApplications > 0 && <span>{t("unclassified", { count: data.unclassifiedApplications })}</span>}
      </div>
    </div>
  );
}

function HiddenNote({ hidden, data }: { hidden: SectorResponseRateRow[]; data: SectorResponseRatesResponse }) {
  const t = useTranslations("responseRates");
  const tSectors = useTranslations("benchmark.sectors");
  if (hidden.length === 0) return null;
  return (
    <p className="text-xs text-gray-500 dark:text-gray-500">
      {t("hiddenList", { count: hidden.length, sectors: hidden.map((row) => tSectors(row.sector)).join(", ") })}
      {data.unclassifiedApplications > 0 && <> {t("unclassified", { count: data.unclassifiedApplications })}</>}
    </p>
  );
}
