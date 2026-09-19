"use client";

import type { ReactNode } from "react";
import { useTranslations } from "next-intl";
import { scoreBand, type ScoreBand } from "@/lib/cvScan/findings";
import {
  barDelayMs,
  barsTotalMs,
  categoryBand,
  categoryFillPercent,
  countUpValue,
} from "@/lib/cvScan/categoryBars";
import { useElapsedMs } from "@/hooks/useElapsedMs";
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

/** Fill and track per band, from the theme's status tokens (globals.css): the track is the
 *  lighter step of the fill's own ramp, so the state reads across the whole bar and not only
 *  from the filled part. */
const BAR_COLOR: Record<ScoreBand, { fill: string; track: string }> = {
  good: { fill: "bg-good", track: "bg-good-wash" },
  fair: { fill: "bg-warn", track: "bg-warn-wash" },
  poor: { fill: "bg-crit", track: "bg-crit-wash" },
};

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

  // One key per result: a new scan re-runs the count-up and, through the <ul>'s key, restarts the
  // bars' CSS animation, which otherwise only plays on mount.
  const resultKey = categories.map((c) => `${c.category}:${c.score}/${c.weight}`).join(",");
  const elapsedMs = useElapsedMs(barsTotalMs(categories.length), resultKey);

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("result.categoriesTitle")}</h2>
      <ul key={resultKey} className="flex flex-col gap-3.5">
        {categories.map((category, index) => {
          const band = categoryBand(category.score, category.weight);

          return (
            <li key={category.category} className="flex flex-col gap-1.5">
              <div className="flex items-baseline justify-between gap-3 text-sm">
                <span className="text-gray-700 dark:text-gray-300">{t(`categories.${category.category}`)}</span>
                {/* The count-up shows frames of the response's own subtotal on its way up; the
                    figure it stops on is the one the page adds up (lib/cvScan/categoryBars.ts). */}
                <span className="tabular-nums text-gray-900 dark:text-gray-100">
                  {t("result.categoryScore", {
                    score: countUpValue(category.score, index, elapsedMs),
                    weight: category.weight,
                  })}
                </span>
              </div>
              {/* The bar is the same number again, not a different one: width is the subtotal over
                  the category's own weight, and the colour is the headline's band applied to that
                  ratio. It grows in from the left (aa-bar-grow), one bar after another; the
                  animation is off under prefers-reduced-motion, like the count-up. */}
              <div className={`h-2 w-full rounded-full ${BAR_COLOR[band].track}`}>
                <div
                  className={`aa-bar-grow h-2 rounded-full ${BAR_COLOR[band].fill}`}
                  style={{
                    width: `${categoryFillPercent(category.score, category.weight)}%`,
                    animationDelay: `${barDelayMs(index)}ms`,
                  }}
                />
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
