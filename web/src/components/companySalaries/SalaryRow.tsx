"use client";

import { useLocale, useTranslations } from "next-intl";
import type { CompanySalaryPublic } from "@/types/api";
import { formatAmount, formatSalaryMonth, occupationName } from "@/lib/companySalaries/salaryDraft";

/**
 * One shared salary as a reader sees it: the occupation (a catalogue name, in the reader's
 * language), the band, the arrangement, the relationship and the month on the left; the monthly
 * net and the bonus on the right. Nothing here names the author or was typed by one, and the
 * years are already a band by the time they reach the browser.
 */
export function SalaryRow({ entry }: { entry: CompanySalaryPublic }) {
  const t = useTranslations("companySalaries.panel");
  const tBands = useTranslations("companySalaries.bands");
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const locale = useLocale();

  return (
    <article className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-col gap-1">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{occupationName(entry.occupation, locale)}</h3>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {[
            t("experience", { band: tBands(entry.experienceBand) }),
            tType(entry.employmentType),
            tStatus(entry.employmentStatus),
            formatSalaryMonth(entry.submittedMonth, locale),
          ].join(" · ")}
        </p>
      </div>
      <div className="flex flex-col items-end gap-0.5">
        <span className="text-base font-semibold text-gray-900 dark:text-gray-100">
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
