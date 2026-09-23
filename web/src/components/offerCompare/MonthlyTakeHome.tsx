"use client";

import { useLocale, useTranslations } from "next-intl";
import { bonusScheduleKey, type Comparison, type Offer } from "@/lib/offerCompare/compare";
import { formatLira, formatNumber } from "@/lib/offerCompare/input";

interface MonthlyTakeHomeProps {
  offers: readonly Offer[];
  comparison: Comparison;
  /** The offer the phone-width chart shows; the wide chart shows them all. */
  chartIndex: number;
  onChartIndexChange: (index: number) => void;
}

/** Vertical bars are this tall at the year's biggest month. */
const BAR_HEIGHT = 150;
/** Below this many pixels the amount no longer fits inside its bar and sits above it instead. */
const INSIDE_MIN_HEIGHT = 26;
/** The same rule for the horizontal bars, as a share of the row. */
const INSIDE_MIN_SHARE = 0.4;

function monthNames(locale: string, month: "short" | "long"): string[] {
  const format = new Intl.DateTimeFormat(locale === "tr" ? "tr-TR" : "en-GB", { month, timeZone: "UTC" });
  return Array.from({ length: 12 }, (_, index) => format.format(new Date(Date.UTC(2026, index, 15))));
}

/**
 * What lands in the account each month, for every offer on one scale — so a gross offer's slide
 * through the brackets and a bonus month's jump are both visible. Each bar carries its own
 * amount (the design canvas's final, 2026-09-24): twelve vertical bars side by side where there
 * is room, one offer at a time as horizontal rows on a phone, where twelve columns would leave no
 * width for the figures.
 */
export function MonthlyTakeHome({ offers, comparison, chartIndex, onChartIndexChange }: MonthlyTakeHomeProps) {
  const t = useTranslations("offerCompare.months");
  const locale = useLocale();
  const short = monthNames(locale, "short");
  const long = monthNames(locale, "long");
  const scale = Math.max(1, comparison.maxMonth);
  const lira = (value: number) => formatLira(value, locale);

  const note = (index: number) => {
    const offer = offers[index];
    const result = comparison.results[index];
    const salaryOnly = result.months.map((month) => month.net - month.bonus);
    const main =
      offer.basis === "net"
        ? t("noteNet", { amount: lira(offer.salary) })
        : t("noteGross", { first: lira(salaryOnly[0]), last: lira(salaryOnly[11]) });
    const bonusKey = bonusScheduleKey(offer.bonusSalaries);
    return bonusKey && result.bonusNet > 0 ? `${main} ${t(bonusKey)}` : main;
  };

  const spoken = (index: number, month: number) => {
    const { net, bonus } = comparison.results[index].months[month];
    return bonus > 0
      ? t("barWithBonus", { month: long[month], amount: lira(net), bonus: lira(bonus) })
      : t("bar", { month: long[month], amount: lira(net) });
  };

  const shown = Math.min(chartIndex, offers.length - 1);

  return (
    <section className="flex flex-col gap-6 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-col gap-1">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      {/* Wide: every offer, twelve columns each. */}
      <div className="hidden flex-col gap-8 lg:flex">
        {offers.map((offer, index) => (
          <div key={index} className="flex flex-col gap-3">
            <div className="flex items-baseline justify-between gap-4">
              <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{offer.name}</h3>
              <p className="text-right text-sm text-gray-600 dark:text-gray-400">{note(index)}</p>
            </div>
            <ol aria-label={t("chartLabel", { name: offer.name })} className="grid grid-cols-12 items-end gap-1.5">
              {comparison.results[index].months.map((month, m) => {
                const baseHeight = ((month.net - month.bonus) / scale) * BAR_HEIGHT;
                const bonusHeight = (month.bonus / scale) * BAR_HEIGHT;
                const inside = baseHeight >= INSIDE_MIN_HEIGHT;
                const label = formatNumber(month.net, locale);
                return (
                  <li key={m} className="flex flex-col items-stretch gap-1.5">
                    <span className="sr-only">{spoken(index, m)}</span>
                    <div aria-hidden="true" className="flex flex-col justify-end" style={{ height: BAR_HEIGHT + (inside ? 0 : 18) }}>
                      {!inside && month.net > 0 && (
                        <span className="pb-1 text-center text-xs font-semibold text-gray-700 tabular-nums dark:text-gray-300">{label}</span>
                      )}
                      {bonusHeight > 0 && <div className="rounded-t bg-accent/40" style={{ height: bonusHeight }} />}
                      <div
                        className={`flex items-end justify-center bg-accent pb-2 ${bonusHeight > 0 ? "" : "rounded-t"}`}
                        style={{ height: baseHeight }}
                      >
                        {inside && (
                          <span className="text-xs font-semibold text-white tabular-nums dark:text-gray-950">{label}</span>
                        )}
                      </div>
                    </div>
                    <span aria-hidden="true" className="text-center text-xs text-gray-500 dark:text-gray-400">
                      {short[m]}
                    </span>
                  </li>
                );
              })}
            </ol>
          </div>
        ))}
      </div>

      {/* Narrow: one offer at a time, a row per month. */}
      <div className="flex flex-col gap-4 lg:hidden">
        <div
          role="group"
          aria-label={t("pick")}
          className="flex gap-0.5 rounded-md border border-gray-200 bg-gray-100 p-0.5 dark:border-gray-800 dark:bg-gray-800/60"
        >
          {offers.map((offer, index) => (
            <button
              key={index}
              type="button"
              aria-pressed={index === shown}
              onClick={() => onChartIndexChange(index)}
              className={`h-11 min-w-0 flex-1 truncate rounded px-2 text-sm font-semibold transition-colors ${
                index === shown ? "bg-accent text-white" : "text-gray-600 dark:text-gray-400"
              }`}
            >
              {offer.name}
            </button>
          ))}
        </div>
        <ol aria-label={t("chartLabel", { name: offers[shown].name })} className="flex flex-col gap-1.5">
          {comparison.results[shown].months.map((month, m) => {
            const baseShare = (month.net - month.bonus) / scale;
            const inside = baseShare >= INSIDE_MIN_SHARE;
            const label = lira(month.net);
            return (
              <li key={m} className="grid grid-cols-[2.25rem_minmax(0,1fr)] items-center gap-2">
                <span className="sr-only">{spoken(shown, m)}</span>
                <span aria-hidden="true" className="text-xs text-gray-500 dark:text-gray-400">
                  {short[m]}
                </span>
                <div aria-hidden="true" className="flex h-8 items-center">
                  <div
                    className={`flex h-full items-center bg-accent pl-2 ${month.bonus > 0 ? "rounded-l" : "rounded"}`}
                    style={{ width: `${baseShare * 100}%` }}
                  >
                    {inside && (
                      <span className="text-[13px] font-semibold whitespace-nowrap text-white tabular-nums dark:text-gray-950">{label}</span>
                    )}
                  </div>
                  {month.bonus > 0 && (
                    <div className="h-full rounded-r bg-accent/40" style={{ width: `${(month.bonus / scale) * 100}%` }} />
                  )}
                  {!inside && month.net > 0 && (
                    <span className="pl-2 text-[13px] font-semibold whitespace-nowrap text-gray-700 tabular-nums dark:text-gray-300">
                      {label}
                    </span>
                  )}
                </div>
              </li>
            );
          })}
        </ol>
        <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">{note(shown)}</p>
      </div>
    </section>
  );
}
