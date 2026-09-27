"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { SalaryLevel, SalaryOccupationYear, SalaryStats } from "@/types/api";
import { LEVELS, LEVEL_STYLES } from "@/lib/salaryMarket/levelStyles";
import {
  bandPath,
  formatChange,
  formatStat,
  linePath,
  percentChange,
  valueAxis,
  xAt,
  yAt,
  type Plot,
} from "@/lib/salaryMarket/chart";

const PLOT: Plot = { width: 1000, height: 340, left: 64, right: 150, top: 12, bottom: 32 };

type Mode = "all" | "levels";

/**
 * The occupation page's chart (canvas "Detay A"): every survey year's median with the middle half
 * as a band, or one line per level. Pointing at a year — or tabbing to it — opens a card with that
 * year's figures and moves the distribution strip under the chart to it. The table further down
 * the page carries every number again, for anyone who cannot use the chart.
 */
export function SalaryTrendChart({ years }: { years: readonly SalaryOccupationYear[] }) {
  const t = useTranslations("salaryMarket.chart");
  const tLevel = useTranslations("salaryMarket.levels");
  const locale = useLocale();
  const [mode, setMode] = useState<Mode>("all");
  const [hover, setHover] = useState<number | null>(null);

  const count = years.length;
  const levelLine = (level: SalaryLevel) =>
    years.map((year, index) => ({ index, stats: year.levels.find((l) => l.level === level)?.stats }));

  const maxValue =
    mode === "all"
      ? Math.max(...years.map((y) => y.overall.p75))
      : Math.max(...years.flatMap((y) => y.levels.map((l) => l.stats.p50)), 1);
  const axis = valueAxis(maxValue);
  const x = (index: number) => xAt(PLOT, index, count);
  const y = (value: number) => yAt(PLOT, value, axis.max);

  const upper = years.map((year, index) => ({ x: x(index), y: y(year.overall.p75) }));
  const lower = years.map((year, index) => ({ x: x(index), y: y(year.overall.p25) }));
  const median = years.map((year, index) => ({ x: x(index), y: y(year.overall.p50) }));

  const shown = hover ?? count - 1;
  const shownYear = years[shown];
  const previous = shown > 0 ? years[shown - 1] : undefined;
  const change = percentChange(shownYear.overall.p50, previous?.overall.p50);
  const tipLeft = (x(shown) / PLOT.width) * 100;
  const tipOnLeft = tipLeft > 62;

  const tabClass = (active: boolean) =>
    `h-9 rounded-md px-3 text-sm font-medium transition-colors ${
      active
        ? "bg-white text-gray-900 shadow-sm dark:bg-gray-700 dark:text-gray-100"
        : "text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
    }`;

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <div role="tablist" aria-label={t("viewLabel")} className="flex gap-1 rounded-lg bg-gray-100 p-1 dark:bg-gray-800">
          <button type="button" role="tab" aria-selected={mode === "all"} className={tabClass(mode === "all")} onClick={() => setMode("all")}>
            {t("viewAll")}
          </button>
          <button type="button" role="tab" aria-selected={mode === "levels"} className={tabClass(mode === "levels")} onClick={() => setMode("levels")}>
            {t("viewLevels")}
          </button>
        </div>
      </div>

      <div className="flex flex-wrap gap-x-5 gap-y-2 text-sm text-gray-600 dark:text-gray-400">
        {mode === "all" ? (
          <>
            <span className="flex items-center gap-2">
              <span aria-hidden="true" className="h-0.5 w-5 bg-accent" />
              {t("median")}
            </span>
            <span className="flex items-center gap-2">
              <span aria-hidden="true" className="h-2.5 w-5 rounded-sm bg-accent/15" />
              {t("middleHalf")}
            </span>
          </>
        ) : (
          LEVELS.map((level) => (
            <span key={level} className="flex items-center gap-2">
              <span aria-hidden="true" className={`h-0.5 w-5 ${LEVEL_STYLES[level].swatch}`} />
              {tLevel(level)}
            </span>
          ))
        )}
      </div>

      <div className="relative w-full">
        <svg
          viewBox={`0 0 ${PLOT.width} ${PLOT.height}`}
          className="h-auto w-full overflow-visible"
          role="img"
          aria-label={t("chartLabel", { first: years[0].year, last: years[count - 1].year })}
          onMouseLeave={() => setHover(null)}
        >
          {axis.ticks.map((tick) => (
            <g key={tick}>
              <line x1={PLOT.left} x2={PLOT.width - PLOT.right + 16} y1={y(tick)} y2={y(tick)} className="stroke-gray-200 dark:stroke-gray-800" strokeWidth={1} />
              <text x={PLOT.left - 10} y={y(tick) + 4} textAnchor="end" className="fill-gray-500 text-[12px] dark:fill-gray-400">
                {tick === 0 ? "0" : t("thousands", { value: Math.round(tick / 1000) })}
              </text>
            </g>
          ))}
          {years.map((year, index) => (
            <text key={year.year} x={x(index)} y={PLOT.height - 8} textAnchor="middle" className="fill-gray-600 text-[12px] dark:fill-gray-400">
              {year.year}
            </text>
          ))}

          {hover !== null && (
            <line x1={x(hover)} x2={x(hover)} y1={PLOT.top} y2={PLOT.height - PLOT.bottom} className="stroke-gray-400 dark:stroke-gray-600" strokeWidth={1} />
          )}

          {mode === "all" ? (
            <>
              <path d={bandPath(upper, lower)} className="fill-accent/15" />
              <path d={linePath(median)} fill="none" className="stroke-accent" strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
              {median.map((point, index) => (
                <circle key={index} cx={point.x} cy={point.y} r={hover === index ? 7 : 5} className="fill-accent stroke-white dark:stroke-gray-900" strokeWidth={2} />
              ))}
              <text x={median[count - 1].x + 14} y={median[count - 1].y + 4} className="fill-gray-900 text-[13px] font-semibold dark:fill-gray-100">
                {formatStat(years[count - 1].overall, "p50", locale)}
              </text>
            </>
          ) : (
            LEVELS.map((level) => {
              const points = levelLine(level)
                .filter((p): p is { index: number; stats: SalaryStats } => p.stats !== undefined)
                .map((p) => ({ index: p.index, x: x(p.index), y: y(p.stats.p50), stats: p.stats }));
              if (points.length === 0) return null;
              const last = points[points.length - 1];
              return (
                <g key={level}>
                  <path d={linePath(points)} fill="none" className={LEVEL_STYLES[level].stroke} strokeWidth={2} strokeLinejoin="round" strokeLinecap="round" />
                  {points.map((point) => (
                    <circle
                      key={point.index}
                      cx={point.x}
                      cy={point.y}
                      r={hover === point.index ? 6.5 : 4.5}
                      className={`${LEVEL_STYLES[level].fill} stroke-white dark:stroke-gray-900`}
                      strokeWidth={2}
                    />
                  ))}
                  {last.index === count - 1 && (
                    <text x={last.x + 14} y={last.y + 4} className="fill-gray-900 text-[13px] font-semibold dark:fill-gray-100">
                      {tLevel(level)} {formatStat(last.stats, "p50", locale)}
                    </text>
                  )}
                </g>
              );
            })
          )}

          {/* One wide, focusable target per year: easier to hit than a dot, and reachable with Tab. */}
          {years.map((year, index) => {
            const half = count > 1 ? (x(1) - x(0)) / 2 : 40;
            return (
              <rect
                key={year.year}
                x={x(index) - half}
                y={0}
                width={half * 2}
                height={PLOT.height - PLOT.bottom + 20}
                fill="transparent"
                tabIndex={0}
                aria-label={t("yearLabel", { year: year.year, median: formatStat(year.overall, "p50", locale), count: year.overall.count })}
                onMouseEnter={() => setHover(index)}
                onFocus={() => setHover(index)}
                onBlur={() => setHover(null)}
                className="cursor-crosshair outline-none focus-visible:fill-accent/5"
              />
            );
          })}
        </svg>

        {hover !== null && (
          <div
            role="status"
            className="pointer-events-none absolute top-2 w-60 rounded-xl border border-gray-200 bg-white p-4 shadow-lg dark:border-gray-700 dark:bg-gray-900"
            style={tipOnLeft ? { right: `calc(${100 - tipLeft}% + 16px)` } : { left: `calc(${tipLeft}% + 16px)` }}
          >
            <div className="flex items-baseline justify-between">
              <span className="text-base font-bold text-gray-900 dark:text-gray-100">{shownYear.year}</span>
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("people", { count: shownYear.overall.count })}</span>
            </div>
            <dl className="mt-2 flex flex-col gap-1.5 text-sm tabular-nums">
              {mode === "all"
                ? (["p75", "p50", "p25"] as const).map((key) => (
                    <div key={key} className="flex items-center justify-between gap-3">
                      <dt className="text-gray-600 dark:text-gray-400">{t(key)}</dt>
                      <dd className="font-semibold text-gray-900 dark:text-gray-100">{formatStat(shownYear.overall, key, locale)}</dd>
                    </div>
                  ))
                : [...LEVELS].reverse().map((level) => {
                    const stats = shownYear.levels.find((l) => l.level === level)?.stats;
                    return (
                      <div key={level} className="flex items-center justify-between gap-3">
                        <dt className="flex items-center gap-2 text-gray-600 dark:text-gray-400">
                          <span aria-hidden="true" className={`h-2.5 w-2.5 rounded-full ${LEVEL_STYLES[level].swatch}`} />
                          {tLevel(level)}
                        </dt>
                        <dd className="font-semibold text-gray-900 dark:text-gray-100">
                          {stats ? formatStat(stats, "p50", locale) : t("notEnough")}
                        </dd>
                      </div>
                    );
                  })}
            </dl>
            {change !== null && (
              <p className="mt-2 border-t border-gray-100 pt-2 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                {t("changeFromPrevious")} <strong className="font-semibold text-good-ink">{formatChange(change, locale)}</strong>
              </p>
            )}
          </div>
        )}
      </div>

      <ul className="flex flex-wrap gap-2 pl-0 sm:pl-16" aria-label={t("changesLabel")}>
        {years.slice(1).map((year, index) => {
          const yearChange = percentChange(year.overall.p50, years[index].overall.p50);
          if (yearChange === null) return null;
          return (
            <li key={year.year} className="rounded-md bg-gray-100 px-2.5 py-1.5 text-sm tabular-nums dark:bg-gray-800">
              <span className="text-gray-500 dark:text-gray-400">{year.year}</span>{" "}
              <strong className="font-semibold text-good-ink">{formatChange(yearChange, locale)}</strong>
            </li>
          );
        })}
      </ul>
      <p className="text-sm text-gray-500 dark:text-gray-400">{t("nominalNote")}</p>

      <DistributionStrip year={shownYear.year} stats={shownYear.overall} />
    </section>
  );
}

/** P10 → P90 of one year: the lowest and highest tenth stand in for the raw extremes, which are
 *  either mistakes or one person (DECISIONS.md 2026-09-27). */
function DistributionStrip({ year, stats }: { year: number; stats: SalaryStats }) {
  const t = useTranslations("salaryMarket.chart");
  const locale = useLocale();
  const max = stats.p90 * 1.1;
  const pct = (value: number) => `${Math.min(100, (value / max) * 100)}%`;
  const cells = [
    ["p10", "bottomTenth"],
    ["p25", "p25"],
    ["p50", "median"],
    ["p75", "p75"],
    ["p90", "topTenth"],
  ] as const;

  return (
    <div className="flex flex-col gap-3 border-t border-gray-100 pt-5 dark:border-gray-800">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100">
          {t("distributionTitle", { year })} <span className="font-normal text-gray-500 dark:text-gray-400">· {t("distributionHint")}</span>
        </h3>
        <span className="text-sm text-gray-500 dark:text-gray-400">{t("people", { count: stats.count })}</span>
      </div>
      <div className="relative mx-2 h-8" aria-hidden="true">
        <div className="absolute inset-x-0 top-3.5 h-1 rounded-full bg-gray-200 dark:bg-gray-800" />
        <div className="absolute top-3.5 h-1 rounded-full bg-accent/30" style={{ left: pct(stats.p10), width: `calc(${pct(stats.p90)} - ${pct(stats.p10)})` }} />
        <div className="absolute top-2 h-4 rounded bg-accent/50" style={{ left: pct(stats.p25), width: `calc(${pct(stats.p75)} - ${pct(stats.p25)})` }} />
        <div className="absolute top-0.5 h-7 w-1 rounded bg-accent-strong" style={{ left: pct(stats.p50) }} />
      </div>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-5">
        {cells.map(([key, label]) => (
          <div key={key} className="flex flex-col gap-0.5">
            <dt className="text-xs text-gray-500 dark:text-gray-400">{t(label)}</dt>
            <dd className="text-base font-semibold text-gray-900 tabular-nums dark:text-gray-100">{formatStat(stats, key, locale)}</dd>
          </div>
        ))}
      </dl>
      <p className="text-xs leading-relaxed text-gray-500 dark:text-gray-400">{t("extremesNote")}</p>
    </div>
  );
}
