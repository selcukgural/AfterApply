"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CompanyReviewSummary } from "@/types/api";
import { distributionFractions, formatScore, reviewsUntilScore } from "@/lib/companyReviews/score";
import { StarRating } from "@/components/companyReviews/StarRating";

const CATEGORY_AVERAGES = [
  ["managementRating", "averageManagement"],
  ["workEnvironmentRating", "averageWorkEnvironment"],
  ["salaryAndBenefitsRating", "averageSalaryAndBenefits"],
  ["careerAndDevelopmentRating", "averageCareerAndDevelopment"],
] as const;

/**
 * The aggregate. Two rules it keeps visibly: no score below the threshold (the count and the gap
 * are shown instead), and the sample size next to every number, so a 4.8 from three people reads
 * as what it is.
 */
/**
 * `showScoringLink={false}` and `compact` are for the landing page's mock: a `role="img"` region must
 * hold nothing focusable, and at half the page's width the score sits above the breakdown rather
 * than beside it, so the category labels keep to one line.
 */
export function ReviewSummaryPanel({
  summary,
  showScoringLink = true,
  compact = false,
}: {
  summary: CompanyReviewSummary;
  showScoringLink?: boolean;
  compact?: boolean;
}) {
  const t = useTranslations("companies.summary");
  const tScoring = useTranslations("companies.scoring");
  const tRatings = useTranslations("companyReviews.ratings");
  const locale = useLocale();
  const fractions = distributionFractions(summary.distribution);
  const remaining = reviewsUntilScore(summary);

  return (
    <section
      className={`grid gap-6 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900 ${compact ? "" : "sm:grid-cols-[auto_1fr]"}`}
    >
      <div className={`flex gap-1 ${compact ? "flex-row flex-wrap items-baseline gap-x-3" : "flex-col items-start"}`}>
        {summary.score !== null ? (
          <>
            <span className="text-5xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
              {formatScore(summary.score, locale)}
            </span>
            <StarRating value={summary.score} label={t("scoreLabel", { score: formatScore(summary.score, locale) })} size="lg" />
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {t("basedOn", { count: summary.approvedCount })}
            </span>
          </>
        ) : (
          <>
            <span className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("noScoreYet")}</span>
            <span className="max-w-[28ch] text-xs text-gray-500 dark:text-gray-400">
              {summary.approvedCount === 0
                ? t("noReviewsYet")
                : t("untilScore", { count: summary.approvedCount, remaining })}
            </span>
          </>
        )}
        {showScoringLink ? (
          <Link href="/companies/scoring" className="mt-1 text-xs text-accent-ink underline-offset-2 hover:underline">
            {tScoring("linkLabel")}
          </Link>
        ) : null}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <dl className="flex flex-col gap-2 text-sm">
          {CATEGORY_AVERAGES.map(([ratingKey, averageKey]) => {
            const average = summary[averageKey];
            return (
              <div key={ratingKey} className="flex items-center justify-between gap-3">
                <dt className="text-gray-600 dark:text-gray-400">{tRatings(ratingKey)}</dt>
                <dd className="flex items-center gap-2">
                  {average === null ? (
                    <span className="text-gray-400">—</span>
                  ) : (
                    <>
                      <StarRating value={average} label={`${tRatings(ratingKey)}: ${formatScore(average, locale)}`} />
                      <span className="w-7 text-right font-medium text-gray-900 dark:text-gray-100">{formatScore(average, locale)}</span>
                    </>
                  )}
                </dd>
              </div>
            );
          })}
        </dl>

        <div className="flex flex-col gap-1.5" aria-label={t("distributionLabel")}>
          {[5, 4, 3, 2, 1].map((star) => (
            <div key={star} className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
              <span className="w-3 text-right">{star}</span>
              <span className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
                <span className="block h-full rounded-full bg-amber-500" style={{ width: `${fractions[star - 1] * 100}%` }} />
              </span>
              <span className="w-6 text-right tabular-nums">{summary.distribution[star - 1] ?? 0}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
