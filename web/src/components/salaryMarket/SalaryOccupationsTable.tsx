"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { SalaryOccupationSummary } from "@/types/api";
import { formatChange, formatStat, percentChange, rangeBar, sparkline } from "@/lib/salaryMarket/chart";
import { salaryOccupationPath } from "@/lib/salaryMarket/path";

const SPARK = { width: 132, height: 30, pad: 6 };

/**
 * The occupations list (canvas "Liste A"): one row per occupation, highest median first, with the
 * middle half as a bar on one shared scale and the median's history as a line on the row's own.
 * The whole row links to the occupation's page.
 */
export function SalaryOccupationsTable({ occupations, years }: { occupations: readonly SalaryOccupationSummary[]; years: readonly number[] }) {
  const t = useTranslations("salaryMarket.list.table");
  const locale = useLocale();
  const rows = [...occupations].sort((a, b) => b.latest.p50 - a.latest.p50);
  // A round number above the widest upper quartile, so every bar is on the same ruler.
  const scale = Math.ceil(Math.max(...rows.map((r) => r.latest.p75), 1) / 50_000) * 50_000 * 1.05;
  const latestYear = Math.max(...rows.map((r) => r.latestYear));

  return (
    <div className="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <div className="hidden grid-cols-[2.2fr_1fr_2.2fr_0.8fr_1.3fr_0.9fr] gap-4 bg-gray-50 px-6 py-3 text-xs font-semibold tracking-wide text-gray-600 uppercase md:grid dark:bg-gray-950 dark:text-gray-400">
        <div>{t("occupation")}</div>
        <div className="text-right">{t("median", { year: latestYear })}</div>
        <div>{t("middleHalf")}</div>
        <div className="text-right">{t("lastYear")}</div>
        <div>{t("trend", { first: years[0], last: years[years.length - 1] })}</div>
        <div className="text-right">{t("responses", { year: latestYear })}</div>
      </div>
      <ul>
        {rows.map((row) => {
          const bar = rangeBar(row.latest, scale);
          const change = percentChange(row.latest.p50, row.previousYearP50);
          const name = locale === "tr" ? row.nameTr : row.nameEn;
          return (
            <li key={row.slug} className="border-t border-gray-100 first:border-t-0 dark:border-gray-800">
              <Link
                href={salaryOccupationPath(locale, row.slug)}
                className="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 px-4 py-3 text-gray-900 hover:bg-gray-50 md:grid-cols-[2.2fr_1fr_2.2fr_0.8fr_1.3fr_0.9fr] md:px-6 dark:text-gray-100 dark:hover:bg-gray-800/50"
              >
                <span className="font-medium">
                  {name}
                  {row.latestYear !== latestYear && (
                    <span className="ml-2 text-xs font-normal text-gray-500 dark:text-gray-400">{t("asOf", { year: row.latestYear })}</span>
                  )}
                </span>
                <span className="text-right font-semibold tabular-nums">{formatStat(row.latest, "p50", locale)}</span>
                <span
                  className="relative col-span-2 h-5 md:col-span-1"
                  title={t("rangeTitle", { p25: formatStat(row.latest, "p25", locale), p75: formatStat(row.latest, "p75", locale) })}
                >
                  <span aria-hidden="true" className="absolute inset-x-0 top-[9px] h-0.5 bg-gray-200 dark:bg-gray-800" />
                  <span aria-hidden="true" className="absolute top-1 h-3 rounded bg-accent/40" style={{ left: `${bar.left}%`, width: `${bar.width}%` }} />
                  <span aria-hidden="true" className="absolute top-0 h-5 w-[3px] rounded-sm bg-accent-strong" style={{ left: `${bar.median}%` }} />
                  <span className="sr-only">{t("rangeTitle", { p25: formatStat(row.latest, "p25", locale), p75: formatStat(row.latest, "p75", locale) })}</span>
                </span>
                <span className="hidden text-right text-sm text-good-ink tabular-nums md:block">{change === null ? "—" : formatChange(change, locale)}</span>
                <Sparkline row={row} years={years} name={name} />
                <span className="hidden text-right text-sm text-gray-600 tabular-nums md:block dark:text-gray-400">
                  {new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB").format(row.latest.count)}
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

/** A median history with a hover dot and a one-line card: the year and its median. */
function Sparkline({ row, years, name }: { row: SalaryOccupationSummary; years: readonly number[]; name: string }) {
  const t = useTranslations("salaryMarket.list.table");
  const locale = useLocale();
  const [hover, setHover] = useState<number | null>(null);
  const line = sparkline(row.trend, years, SPARK);
  const shown = line.points.find((p) => p.year === hover) ?? line.points[line.points.length - 1];
  const step = years.length > 1 ? (SPARK.width - SPARK.pad * 2) / (years.length - 1) : SPARK.width;

  return (
    <span className="relative hidden h-[30px] w-[132px] md:block">
      <svg width={SPARK.width} height={SPARK.height} viewBox={`0 0 ${SPARK.width} ${SPARK.height}`} role="img" aria-label={t("trendLabel", { occupation: name })} onMouseLeave={() => setHover(null)}>
        <path d={line.path} fill="none" className="stroke-accent" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
        {shown && <circle cx={shown.x} cy={shown.y} r={4} className="fill-accent stroke-white dark:stroke-gray-900" strokeWidth={2} />}
        {line.points.map((point) => (
          <rect key={point.year} x={point.x - step / 2} y={0} width={step} height={SPARK.height} fill="transparent" onMouseEnter={() => setHover(point.year)} />
        ))}
      </svg>
      {hover !== null && shown && (
        <span
          role="status"
          className="pointer-events-none absolute bottom-9 left-1/2 z-10 -translate-x-1/2 rounded-md bg-gray-900 px-2.5 py-1.5 text-xs whitespace-nowrap text-white tabular-nums shadow dark:bg-gray-100 dark:text-gray-900"
        >
          <strong>{shown.year}</strong> · {t("medianShort")} {formatStat({ ...row.latest, p50: shown.p50, atLeast: [] }, "p50", locale)}
        </span>
      )}
    </span>
  );
}
