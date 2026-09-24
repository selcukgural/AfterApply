"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { SalaryBandBar } from "@/components/companySalaries/SalaryBandBar";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { formatAmount } from "@/lib/companySalaries/salaryDraft";
import { positionKey } from "@/lib/contributionLoop/contributionLoop";

interface SalaryPositionPanelProps {
  entryId: string;
  /** "saved": the full panel under the save banner. "card": one line and the bar, inside the
   *  author's own entry on the contributions page. */
  variant: "saved" | "card";
}

export const salaryPositionQueryKey = (entryId: string) => ["companySalaries", "position", entryId] as const;

/**
 * Where the author's own salary sits in the company's current band (contribution loop #9, canvas
 * 1 + 4 + 3, 2026-09-24). Against the band's median and range — the norm, never a ranking among
 * people — over the same rows the company page's band is made of. Below its threshold (five
 * rows, above the company page's three: at three the range hands over the others' amounts) it
 * says how many there are and where the view will appear once there are enough.
 */
export function SalaryPositionPanel({ entryId, variant }: SalaryPositionPanelProps) {
  const t = useTranslations("companySalaries.position");
  const locale = useLocale();
  const position = useQuery({
    queryKey: salaryPositionQueryKey(entryId),
    queryFn: () => companySalariesApi.position(entryId),
  });

  const data = position.data;
  if (!data) return null;

  const amount = (value: number) => formatAmount(locale, value, data.currency);
  const key = positionKey(data.percentFromMedian);
  const open = data.medianMonthlyNet !== null && data.minMonthlyNet !== null && data.maxMonthlyNet !== null && key !== null;
  const bar = open ? (
    <SalaryBandBar
      min={data.minMonthlyNet!}
      max={data.maxMonthlyNet!}
      median={data.medianMonthlyNet!}
      value={data.monthlyNetAmount}
      currency={data.currency}
      compact={variant === "card"}
    />
  ) : null;
  const sentence = open
    ? t(`sentence.${key}`, {
        amount: amount(data.monthlyNetAmount),
        count: data.count,
        percent: Math.abs(data.percentFromMedian!),
      })
    : null;

  if (variant === "card") {
    return (
      <div className="flex flex-col gap-2 rounded-lg bg-gray-50 p-3 dark:bg-gray-800/60">
        {open ? (
          <>
            <span className="text-xs text-gray-600 dark:text-gray-400">{t("cardLabel", { count: data.count })}</span>
            <p className="sr-only">{sentence}</p>
            {bar}
          </>
        ) : (
          <span className="text-xs text-gray-600 dark:text-gray-400">
            {t("cardClosed", { count: data.count, minimum: data.minimumEntries })}
          </span>
        )}
      </div>
    );
  }

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
      {open ? (
        <>
          <div className="flex flex-col gap-1">
            <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title", { company: data.companyName })}</h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">{sentence}</p>
          </div>
          {bar}
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {t(data.includesOwn ? "note" : "noteWithoutOwn", { years: data.windowYears, currency: data.currency })}
          </p>
        </>
      ) : (
        <>
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("closedTitle")}</h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">
            {t("closedBody", { company: data.companyName, count: data.count, minimum: data.minimumEntries, currency: data.currency })}
          </p>
          <div className="flex items-center gap-3" aria-hidden="true">
            <div className="h-2 flex-1 overflow-hidden rounded-full bg-track">
              <div className="h-full bg-accent" style={{ width: `${Math.min(100, (data.count / data.minimumEntries) * 100)}%` }} />
            </div>
            <span className="text-xs font-semibold text-gray-600 tabular-nums dark:text-gray-400">
              {data.count} / {data.minimumEntries}
            </span>
          </div>
        </>
      )}
      {data.companySlug && (
        <div>
          <Link href={`/companies/${data.companySlug}`} className={buttonClassName("outline")}>
            {t("companyLink")}
          </Link>
        </div>
      )}
    </section>
  );
}
