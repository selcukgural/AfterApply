"use client";

import { useLocale, useTranslations } from "next-intl";
import type { CompanyReviewPublic } from "@/types/api";
import { categoryMessageKey, findStatement } from "@/lib/companyReviews/statementCatalogue";
import { formatSubmittedMonth } from "@/lib/companyReviews/score";
import { StarRating } from "@/components/companyReviews/StarRating";
import { StatementTag } from "@/components/companyReviews/StatementChip";

interface ReviewCardProps {
  review: CompanyReviewPublic;
  /** Whether the reader has marked this one helpful. Undefined for a signed-out reader. */
  helpfulMarked?: boolean;
  /** Absent for a signed-out reader: the buttons still render, but as links to sign in. */
  onToggleHelpful?: () => void;
  onReport?: () => void;
  signInHref?: string;
  busy?: boolean;
}

/**
 * One published review: the relationship and month, the overall stars, only the categories the
 * author rated, and the statements they picked as short labels (the full sentence is the
 * tooltip). Nothing here is text the author typed — a legacy review's text is not even on the
 * record — so there is nothing to escape and nothing a reader has to take on trust.
 */
export function ReviewCard({ review, helpfulMarked, onToggleHelpful, onReport, signInHref, busy }: ReviewCardProps) {
  const t = useTranslations("companyReviews.card");
  const tCategories = useTranslations("companyReviews.categories");
  const tStatements = useTranslations("companyReviews.statements");
  const tStatus = useTranslations("employmentStatus");
  const locale = useLocale();

  const ratedCount = review.categoryRatings.length + (review.legacySalaryAndBenefitsRating !== null ? 1 : 0);
  const hasPicks = review.likedStatements.length > 0 || review.improvableStatements.length > 0;

  const tags = (keys: string[]) =>
    keys.flatMap((key) => {
      const statement = findStatement(key);
      // A key the catalogue no longer knows renders as nothing rather than as its key.
      if (!statement) return [];
      return [
        <li key={key}>
          <StatementTag label={tStatements(`${key}.label`)} sentence={tStatements(`${key}.sentence`)} kind={statement.kind} />
        </li>,
      ];
    });

  return (
    <article className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{tStatus(review.employmentStatus)}</h3>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {formatSubmittedMonth(review.submittedMonth, locale)}
            {ratedCount > 0 ? ` · ${t("ratedCategories", { count: ratedCount })}` : null}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <StarRating value={review.overallRating} label={t("overallLabel", { value: review.overallRating })} size="lg" />
          <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{review.overallRating}</span>
        </div>
      </header>

      {ratedCount > 0 ? (
        <dl className="grid gap-x-4 gap-y-1 text-xs text-gray-600 sm:grid-cols-2 dark:text-gray-400">
          {review.categoryRatings.map(({ category, rating }) => (
            <div key={category} className="flex items-center justify-between gap-2">
              <dt>{tCategories(categoryMessageKey(category))}</dt>
              <dd>
                <StarRating value={rating} label={`${tCategories(categoryMessageKey(category))}: ${rating}`} />
              </dd>
            </div>
          ))}
          {review.legacySalaryAndBenefitsRating !== null ? (
            <div className="flex items-center justify-between gap-2">
              <dt>{t("legacySalaryAndBenefits")}</dt>
              <dd>
                <StarRating
                  value={review.legacySalaryAndBenefitsRating}
                  label={`${t("legacySalaryAndBenefits")}: ${review.legacySalaryAndBenefitsRating}`}
                />
              </dd>
            </div>
          ) : null}
        </dl>
      ) : null}

      {hasPicks ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {review.likedStatements.length > 0 ? (
            <section>
              <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-good-ink">{t("liked")}</h4>
              <ul className="flex flex-wrap gap-1.5">{tags(review.likedStatements)}</ul>
            </section>
          ) : null}
          {review.improvableStatements.length > 0 ? (
            <section>
              <h4 className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-warn-ink">{t("improvable")}</h4>
              <ul className="flex flex-wrap gap-1.5">{tags(review.improvableStatements)}</ul>
            </section>
          ) : null}
        </div>
      ) : null}

      {review.format === "Legacy" ? (
        <p className="rounded-lg bg-muted-wash px-3 py-2 text-xs text-muted-ink">{t("legacyNote")}</p>
      ) : null}

      <footer className="flex flex-wrap items-center gap-3 text-xs">
        {onToggleHelpful ? (
          <button
            type="button"
            onClick={onToggleHelpful}
            disabled={busy}
            aria-pressed={helpfulMarked === true}
            className={`rounded-full border px-3 py-1 font-medium transition-colors disabled:opacity-60 ${
              helpfulMarked
                ? "border-accent bg-accent/10 text-accent-ink"
                : "border-gray-300 text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
            }`}
          >
            {t("helpful", { count: review.helpfulCount })}
          </button>
        ) : (
          <a
            href={signInHref}
            className="rounded-full border border-gray-300 px-3 py-1 font-medium text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            {t("helpful", { count: review.helpfulCount })}
          </a>
        )}
        {onReport ? (
          <button type="button" onClick={onReport} disabled={busy} className="text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
            {t("report")}
          </button>
        ) : (
          <a href={signInHref} className="text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
            {t("report")}
          </a>
        )}
      </footer>
    </article>
  );
}
