"use client";

import type { ReactNode } from "react";
import { useTranslations } from "next-intl";
import { scoreBand } from "@/lib/cvScan/findings";
import type { CvScanCategoryScore } from "@/types/api";

/**
 * The two pieces of the result screen that are pure presentation of numbers: the headline score
 * and the category subtotals under it.
 *
 * Split out of CvScanResult (2026-09-12) so the landing page can show the same cards with fixed
 * demo figures rather than a picture of them. That is the standing rule for promo visuals — a
 * component cannot drift from the product the way a screenshot does — and it holds here only
 * because these two render whatever they are given: the arithmetic (the band thresholds, the
 * subtotal-over-weight bar) stays in one place for both callers.
 */

const BAND_COLOR = {
  good: "text-emerald-600 dark:text-emerald-400",
  fair: "text-amber-600 dark:text-amber-400",
  poor: "text-red-600 dark:text-red-400",
} as const;

/** `children` is the footnote area under the band sentence — the real screen puts the ATS
 *  correction there; the landing demo leaves it empty. */
export function CvScanScoreCard({ score, children }: { score: number; children?: ReactNode }) {
  const t = useTranslations("cvScan");
  const band = scoreBand(score);

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <p className="text-sm text-gray-500 dark:text-gray-400">{t("result.scoreLabel")}</p>
      <p className="mt-1 flex items-baseline gap-2">
        <span className={`text-5xl font-semibold tracking-tight tabular-nums ${BAND_COLOR[band]}`}>{score}</span>
        <span className="text-sm text-gray-500 dark:text-gray-400">{t("result.outOf")}</span>
      </p>
      <p className="mt-2 text-sm text-gray-700 dark:text-gray-300">{t(`result.bands.${band}`)}</p>
      {children}
    </section>
  );
}

export function CvScanCategoryBars({ categories }: { categories: CvScanCategoryScore[] }) {
  const t = useTranslations("cvScan");

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.categoriesTitle")}</h2>
      <ul className="flex flex-col gap-3">
        {categories.map((category) => (
          <li key={category.category} className="flex flex-col gap-1">
            <div className="flex items-baseline justify-between gap-3 text-sm">
              <span className="text-gray-700 dark:text-gray-300">{t(`categories.${category.category}`)}</span>
              <span className="tabular-nums text-gray-900 dark:text-gray-100">
                {t("result.categoryScore", { score: category.score, weight: category.weight })}
              </span>
            </div>
            {/* The bar is the same number again, not a different one: width is the subtotal over
                the category's own weight. */}
            <div className="h-1.5 w-full rounded-full bg-gray-200 dark:bg-gray-800">
              <div
                className="h-1.5 rounded-full bg-gray-900 dark:bg-gray-100"
                style={{ width: `${(category.score / category.weight) * 100}%` }}
              />
            </div>
          </li>
        ))}
      </ul>
    </section>
  );
}
