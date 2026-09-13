"use client";

import { useLocale, useTranslations } from "next-intl";
import type { CompanyReviewPublic } from "@/types/api";
import { RATING_KEYS } from "@/lib/companyReviews/reviewDraft";
import { formatSubmittedMonth } from "@/lib/companyReviews/score";
import { StarRating } from "@/components/companyReviews/StarRating";

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
 * One published review. The text is rendered as text (`whitespace-pre-wrap`), never as HTML: it is
 * written by a stranger about a named company, which is the most untrusted string this site holds.
 */
export function ReviewCard({ review, helpfulMarked, onToggleHelpful, onReport, signInHref, busy }: ReviewCardProps) {
  const t = useTranslations("companyReviews.card");
  const tRatings = useTranslations("companyReviews.ratings");
  const tStatus = useTranslations("employmentStatus");
  const locale = useLocale();

  return (
    <article className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <header className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100">{review.title}</h3>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {tStatus(review.employmentStatus)} · {formatSubmittedMonth(review.submittedMonth, locale)}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <StarRating value={review.overallRating} label={t("overallLabel", { value: review.overallRating })} size="lg" />
          <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{review.overallRating}</span>
        </div>
      </header>

      <dl className="grid gap-x-4 gap-y-1 text-xs text-gray-600 sm:grid-cols-2 dark:text-gray-400">
        {RATING_KEYS.filter((key) => key !== "overallRating").map((key) => (
          <div key={key} className="flex items-center justify-between gap-2">
            <dt>{tRatings(key)}</dt>
            <dd>
              <StarRating value={review[key]} label={`${tRatings(key)}: ${review[key]}`} />
            </dd>
          </div>
        ))}
      </dl>

      <div className="grid gap-3 sm:grid-cols-2">
        <section>
          <h4 className="mb-1 text-xs font-semibold uppercase tracking-wide text-green-700 dark:text-green-400">{t("pros")}</h4>
          <p className="whitespace-pre-wrap text-sm text-gray-700 dark:text-gray-300">{review.pros}</p>
        </section>
        <section>
          <h4 className="mb-1 text-xs font-semibold uppercase tracking-wide text-red-700 dark:text-red-400">{t("cons")}</h4>
          <p className="whitespace-pre-wrap text-sm text-gray-700 dark:text-gray-300">{review.cons}</p>
        </section>
      </div>

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
