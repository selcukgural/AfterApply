"use client";

import { useLocale, useTranslations } from "next-intl";
import type { SalaryCurrencyStat } from "@/types/api";
import { formatAmount } from "@/lib/companySalaries/salaryDraft";

/**
 * The per-currency figures, one strip per currency that has reached the threshold. A currency
 * under it is not shown here at all — the rows below carry its entries, and a "not enough yet"
 * line under the heading says how many are missing.
 */
export function SalaryStatsStrip({ stats }: { stats: SalaryCurrencyStat[] }) {
  const t = useTranslations("companySalaries.stats");
  const tCurrency = useTranslations("salaryCurrency");
  const locale = useLocale();
  const ready = stats.filter((s) => s.medianMonthlyNet !== null && s.minMonthlyNet !== null && s.maxMonthlyNet !== null);
  if (ready.length === 0) return null;

  return (
    <div className="flex flex-col gap-3">
      {ready.map((stat) => (
        <dl
          key={stat.currency}
          className="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 sm:grid-cols-3 dark:border-gray-800 dark:bg-gray-900"
        >
          <div className="flex flex-col gap-0.5">
            <dt className="text-xs text-gray-500 dark:text-gray-400">{t("median")}</dt>
            <dd className="text-2xl font-semibold text-gray-900 dark:text-gray-100">
              {formatAmount(locale, stat.medianMonthlyNet!, stat.currency)}
            </dd>
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {t("sample", { count: stat.count, currency: tCurrency(stat.currency) })}
            </span>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="text-xs text-gray-500 dark:text-gray-400">{t("range")}</dt>
            <dd className="text-xl font-semibold text-gray-900 dark:text-gray-100">
              {formatAmount(locale, stat.minMonthlyNet!, stat.currency)} – {formatAmount(locale, stat.maxMonthlyNet!, stat.currency)}
            </dd>
            <span className="text-xs text-gray-500 dark:text-gray-400">{t("rangeHint")}</span>
          </div>
          <div className="flex flex-col gap-0.5">
            <dt className="text-xs text-gray-500 dark:text-gray-400">{t("basis")}</dt>
            <dd className="text-sm text-gray-700 dark:text-gray-300">{t("basisBody")}</dd>
          </div>
        </dl>
      ))}
    </div>
  );
}
