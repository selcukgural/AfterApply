"use client";

import { useLocale, useTranslations } from "next-intl";
import type { Comparison } from "@/lib/offerCompare/compare";
import { formatLira } from "@/lib/offerCompare/input";

/** The office-cost stripe: the part of the bar the office days take back. */
export const OFFICE_STRIPE = "repeating-linear-gradient(135deg, var(--crit) 0 3px, var(--crit-wash) 3px 6px)";

interface OfferVerdictProps {
  names: readonly string[];
  comparison: Comparison;
  /** The estimate note and its link to the page's method section; off in the landing page's demo,
   *  where there is no method section to link to and the numbers are labelled sample data. */
  showNote?: boolean;
}

/**
 * The answer first — which offer leaves more and by how much — then one bar per offer, split into
 * what makes up its year. The bar is drawn for the eye; the line under it says the same numbers
 * in words, so the bar itself stays out of the accessibility tree.
 */
export function OfferVerdict({ names, comparison, showNote = true }: OfferVerdictProps) {
  const t = useTranslations("offerCompare.verdict");
  const locale = useLocale();
  const lira = (value: number) => formatLira(value, locale);
  const maybe = (value: number) => (value > 0 ? lira(value) : t("none"));

  const headline =
    comparison.best !== null
      ? t("best", { name: names[comparison.best], amount: lira(comparison.lead) })
      : comparison.tie
        ? t("tie")
        : t("waiting");

  const widest = Math.max(1, ...comparison.results.map((r) => r.salaryNet + r.bonusNet + r.meal + r.perks));
  const percent = (value: number, of: number) => `${Math.max(0, (value / of) * 100)}%`;

  return (
    <section className="flex flex-col gap-5 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
      <p aria-live={showNote ? "polite" : undefined} className="text-xl leading-snug font-semibold tracking-tight text-gray-900 sm:text-2xl dark:text-gray-100">
        {headline}
      </p>

      <ul className="flex flex-col gap-5">
        {comparison.results.map((result, index) => {
          const positive = result.salaryNet + result.bonusNet + result.meal + result.perks;
          const isBest = comparison.best === index;
          return (
            <li key={index} className="flex flex-col gap-2">
              <div className="flex items-baseline justify-between gap-3">
                <span className="min-w-0 truncate text-sm font-semibold text-gray-900 dark:text-gray-100">
                  {names[index]}{" "}
                  <span className="font-normal text-gray-500 dark:text-gray-400">
                    · {t("officeDays", { count: result.officeDaysPerWeek })}
                  </span>
                </span>
                <span
                  className={`shrink-0 text-lg font-semibold tabular-nums ${isBest ? "text-accent-ink" : "text-gray-900 dark:text-gray-100"}`}
                >
                  {lira(result.total)}
                </span>
              </div>
              <div aria-hidden="true" className="flex h-3.5 overflow-hidden rounded bg-track">
                <div className="relative flex" style={{ width: percent(positive, widest) }}>
                  <div className="bg-accent" style={{ width: percent(result.salaryNet, positive || 1) }} />
                  <div className="bg-accent/40" style={{ width: percent(result.bonusNet, positive || 1) }} />
                  <div className="bg-good" style={{ width: percent(result.meal + result.perks, positive || 1) }} />
                  <div
                    className="absolute inset-y-0 right-0"
                    style={{ width: percent(Math.min(result.officeCost, positive), positive || 1), background: OFFICE_STRIPE }}
                  />
                </div>
              </div>
              <span className="text-sm leading-snug text-gray-600 dark:text-gray-400">
                {t("breakdown", {
                  salary: maybe(result.salaryNet),
                  bonus: maybe(result.bonusNet),
                  perks: maybe(result.meal + result.perks),
                  office: result.officeCost > 0 ? `− ${lira(result.officeCost)}` : t("none"),
                })}
              </span>
            </li>
          );
        })}
      </ul>

      <ul aria-hidden="true" className="flex flex-wrap gap-x-4 gap-y-2 text-xs text-gray-600 dark:text-gray-400">
        <li className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-accent" />
          {t("legendSalary")}
        </li>
        <li className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-accent/40" />
          {t("legendBonus")}
        </li>
        <li className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm bg-good" />
          {t("legendPerks")}
        </li>
        <li className="inline-flex items-center gap-1.5">
          <span className="h-2.5 w-2.5 rounded-sm" style={{ background: OFFICE_STRIPE }} />
          {t("legendOffice")}
        </li>
      </ul>

      {/* Next to the number, not only at the foot of the page: whoever reads the verdict reads this. */}
      {showNote && (
        <p className="border-t border-gray-100 pt-4 text-sm text-gray-600 dark:border-gray-800 dark:text-gray-400">
          {t.rich("note", {
            link: (chunks) => (
              <a href="#method" className="font-medium text-accent-ink hover:underline">
                {chunks}
              </a>
            ),
          })}
        </p>
      )}
    </section>
  );
}
