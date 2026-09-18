"use client";

import { useLocale, useTranslations } from "next-intl";
import type { CompanySalaryPublic } from "@/types/api";
import { formatAmount, formatSalaryMonth, formatSalaryPeriod, occupationName } from "@/lib/companySalaries/salaryDraft";

/**
 * One shared salary as a reader sees it: the occupation (a catalogue name, in the reader's
 * language), the band, the arrangement, the relationship and the period on the left; the monthly
 * net and the bonus on the right. Nothing here names the author or was typed by one, and the
 * years are already a band by the time they reach the browser.
 *
 * A row that is not current (design canvas 2026-09-18) is drawn quieter — dashed, on the panel's
 * ground — so a 2012 salary under a 2026 one reads as history, not as a bargain. A row written
 * before the period existed says so and shows the month it was shared instead.
 */
export function SalaryRow({ entry }: { entry: CompanySalaryPublic }) {
  const t = useTranslations("companySalaries.panel");
  const tBands = useTranslations("companySalaries.bands");
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const locale = useLocale();

  const period = formatSalaryPeriod(entry, t("periodOngoing"));
  const previous = !entry.isCurrentPeriod;

  return (
    <article
      className={
        previous
          ? "flex flex-wrap items-center justify-between gap-3 rounded-xl border border-dashed border-gray-300 bg-gray-50 p-4 dark:border-gray-700 dark:bg-gray-900/60"
          : "flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900"
      }
    >
      <div className="flex flex-col gap-1">
        <h3 className="flex flex-wrap items-center gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100">
          {occupationName(entry.occupation, locale)}
          {previous && (
            <span
              className={
                period
                  ? "rounded-full border border-gray-200 bg-white px-2 py-0.5 text-[11px] font-medium text-gray-600 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-300"
                  : "rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 dark:bg-amber-900/30 dark:text-amber-300"
              }
            >
              {period ?? t("periodUnknown")}
            </span>
          )}
        </h3>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {[
            t("experience", { band: tBands(entry.experienceBand) }),
            tType(entry.employmentType),
            tStatus(entry.employmentStatus),
            // The period sits in the title chip on a previous row; the meta line carries it on a
            // current one, and the shared month only when there is no period at all.
            ...(previous ? (period ? [] : [t("sharedIn", { month: formatSalaryMonth(entry.submittedMonth, locale) })]) : [period]),
          ].join(" · ")}
        </p>
      </div>
      <div className="flex flex-col items-end gap-0.5">
        <span className={previous ? "text-base font-semibold text-gray-700 dark:text-gray-300" : "text-base font-semibold text-gray-900 dark:text-gray-100"}>
          {formatAmount(locale, entry.monthlyNetAmount, entry.currency)}{" "}
          <span className="text-xs font-normal text-gray-500 dark:text-gray-400">{t("perMonthNet")}</span>
        </span>
        {entry.annualBonusAmount !== null ? (
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {t("bonus", { amount: formatAmount(locale, entry.annualBonusAmount, entry.currency) })}
          </span>
        ) : (
          <span className="text-xs text-gray-400 dark:text-gray-500">{t("noBonus")}</span>
        )}
      </div>
    </article>
  );
}
