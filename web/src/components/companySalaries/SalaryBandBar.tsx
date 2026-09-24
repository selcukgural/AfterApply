"use client";

import { useLocale, useTranslations } from "next-intl";
import { bandOffset } from "@/lib/contributionLoop/contributionLoop";
import { formatAmount } from "@/lib/companySalaries/salaryDraft";
import type { SalaryCurrency } from "@/types/api";

interface SalaryBandBarProps {
  min: number;
  max: number;
  median: number;
  value: number;
  currency: SalaryCurrency;
  compact?: boolean;
}

/**
 * A company's current band as a bar — minimum to maximum, the median as a tick — with the
 * reader's own amount as a dot on it. Drawn for the eye; the sentence beside it carries the same
 * numbers in words, so the drawing stays out of the accessibility tree.
 */
export function SalaryBandBar({ min, max, median, value, currency, compact = false }: SalaryBandBarProps) {
  const t = useTranslations("companySalaries.position");
  const locale = useLocale();
  const at = (amount: number) => `${bandOffset(amount, min, max)}%`;
  // The median's label is centred on its tick but kept off the bar's ends, where it would run
  // into the minimum or maximum written underneath.
  const labelAt = `${Math.min(80, Math.max(20, bandOffset(median, min, max)))}%`;

  return (
    <div aria-hidden="true" className={`relative mx-2 ${compact ? "h-12" : "h-16"}`}>
      <span
        className="absolute top-0 -translate-x-1/2 text-xs whitespace-nowrap text-gray-600 dark:text-gray-400"
        style={{ left: labelAt }}
      >
        {t("medianLabel", { amount: formatAmount(locale, median, currency) })}
      </span>
      <div className={`absolute inset-x-0 h-2.5 rounded-full bg-track ${compact ? "top-5" : "top-6"}`} />
      <div className={`absolute w-0.5 bg-muted ${compact ? "top-3.5 h-5.5" : "top-4.5 h-6.5"}`} style={{ left: at(median) }} />
      <div
        className={`absolute h-5 w-5 -translate-x-1/2 rounded-full bg-accent ring-3 ring-white dark:ring-gray-900 ${compact ? "top-3.5" : "top-4.5"}`}
        style={{ left: at(value) }}
      />
      <span className="absolute bottom-0 left-0 text-xs text-gray-500 dark:text-gray-400">{formatAmount(locale, min, currency)}</span>
      <span className="absolute right-0 bottom-0 text-xs text-gray-500 dark:text-gray-400">{formatAmount(locale, max, currency)}</span>
    </div>
  );
}
