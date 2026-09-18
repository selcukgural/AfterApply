"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { candidateExperiencesApi } from "@/lib/api/candidateExperiences";
import { formatAmount, occupationName } from "@/lib/companySalaries/salaryDraft";
import { takeRecent } from "@/lib/profile/recent";
import { buttonClassName } from "@/components/ui/Button";
import { StarRating } from "@/components/companyReviews/StarRating";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";

/**
 * The newest few of what the person has written about companies, with the quota, and the doors
 * to the full list. Summary here, management there: editing and deleting stay on /my-reviews —
 * one contributions list for all three kinds since 2026-09-18 — which the Companies menu
 * already points at. Readers of the company pages
 * never see who wrote these; the card says so once.
 *
 * The caller decides whether the card exists (reviews flag) and whether the salaries and the
 * experiences columns are drawn (their flags) — a flag that is off means no request either.
 */
export function ContributionsCard({ showSalaries, showExperiences = false }: { showSalaries: boolean; showExperiences?: boolean }) {
  const t = useTranslations("profile.contributions");
  const tOutcome = useTranslations("hiringOutcome");
  const locale = useLocale();
  const dateFormat = new Intl.DateTimeFormat(locale, { dateStyle: "medium" });

  const reviews = useQuery({ queryKey: ["companyReviews", "mine"], queryFn: companyReviewsApi.listMine });
  const salaries = useQuery({ queryKey: ["companySalaries", "mine"], queryFn: companySalariesApi.listMine, enabled: showSalaries });
  const experiences = useQuery({ queryKey: ["candidateExperiences", "mine"], queryFn: candidateExperiencesApi.listMine, enabled: showExperiences });
  const columns = 1 + (showSalaries ? 1 : 0) + (showExperiences ? 1 : 0);

  return (
    <section className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("anonymous")}</p>
      </div>

      <div className={`grid gap-6 ${columns === 3 ? "md:grid-cols-3" : columns === 2 ? "md:grid-cols-2" : ""}`}>
        <div className="flex flex-col gap-2">
          <h3 className="flex items-baseline justify-between gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100">
            {t("reviews.title")}
            {reviews.data && (
              <span className="text-xs font-normal text-gray-500 tabular-nums dark:text-gray-400">
                {t("quota", { used: reviews.data.quota.used, limit: reviews.data.quota.limit })}
              </span>
            )}
          </h3>
          {reviews.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
          {reviews.isError && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {t("error")}
            </p>
          )}
          {reviews.data && reviews.data.items.length === 0 && (
            <p className="rounded-lg border border-dashed border-gray-300 p-3 text-center text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
              {t("reviews.empty")}
            </p>
          )}
          {reviews.data && reviews.data.items.length > 0 && (
            <ul className="flex flex-col gap-2">
              {takeRecent(reviews.data.items).map((review) => (
                <li
                  key={review.id}
                  className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 px-3 py-2 text-sm dark:border-gray-800"
                >
                  <div className="min-w-0">
                    <Link href={`/companies/${review.companySlug}`} className="block truncate font-medium text-gray-900 underline-offset-2 hover:underline dark:text-gray-100">
                      {review.companyName}
                    </Link>
                    <span className="flex flex-wrap items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                      <span>{dateFormat.format(new Date(review.submittedAt))}</span>
                      <ReviewStatusBadge status={review.status} />
                    </span>
                  </div>
                  <StarRating value={review.overallRating} label={t("reviews.rating", { value: review.overallRating })} />
                </li>
              ))}
            </ul>
          )}
          <div className="mt-1 flex flex-wrap items-center justify-between gap-2">
            <Link href="/my-reviews" className="text-sm text-accent-ink underline-offset-2 hover:underline">
              {t("reviews.all")}
            </Link>
            <Link href="/contribute?tab=review" className={buttonClassName("outline")}>
              {t("reviews.write")}
            </Link>
          </div>
        </div>

        {showSalaries && (
          <div className="flex flex-col gap-2">
            <h3 className="flex items-baseline justify-between gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100">
              {t("salaries.title")}
              {salaries.data && (
                <span className="text-xs font-normal text-gray-500 tabular-nums dark:text-gray-400">
                  {t("quota", { used: salaries.data.quota.used, limit: salaries.data.quota.limit })}
                </span>
              )}
            </h3>
            {salaries.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
            {salaries.isError && (
              <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                {t("error")}
              </p>
            )}
            {salaries.data && salaries.data.items.length === 0 && (
              <p className="rounded-lg border border-dashed border-gray-300 p-3 text-center text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
                {t("salaries.empty")}
              </p>
            )}
            {salaries.data && salaries.data.items.length > 0 && (
              <ul className="flex flex-col gap-2">
                {takeRecent(salaries.data.items).map((entry) => (
                  <li
                    key={entry.id}
                    className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 px-3 py-2 text-sm dark:border-gray-800"
                  >
                    <div className="min-w-0">
                      <Link href={`/companies/${entry.companySlug}?tab=salaries`} className="block truncate font-medium text-gray-900 underline-offset-2 hover:underline dark:text-gray-100">
                        {entry.companyName}
                      </Link>
                      <span className="block truncate text-xs text-gray-500 dark:text-gray-400">
                        {occupationName(entry.occupation, locale)} · {t("salaries.years", { count: entry.yearsOfExperience })}
                      </span>
                    </div>
                    <span className="shrink-0 text-right font-medium text-gray-900 tabular-nums dark:text-gray-100">
                      {formatAmount(locale, entry.monthlyNetAmount, entry.currency)}
                      <span className="block text-xs font-normal text-gray-500 dark:text-gray-400">{t("salaries.perMonthNet")}</span>
                    </span>
                  </li>
                ))}
              </ul>
            )}
            <div className="mt-1 flex flex-wrap items-center justify-between gap-2">
              <Link href="/my-reviews" className="text-sm text-accent-ink underline-offset-2 hover:underline">
                {t("salaries.all")}
              </Link>
              <Link href="/contribute?tab=salary" className={buttonClassName("outline")}>
                {t("salaries.share")}
              </Link>
            </div>
          </div>
        )}

        {showExperiences && (
          <div className="flex flex-col gap-2">
            <h3 className="flex items-baseline justify-between gap-2 text-sm font-semibold text-gray-900 dark:text-gray-100">
              {t("experiences.title")}
              {experiences.data && (
                <span className="text-xs font-normal text-gray-500 tabular-nums dark:text-gray-400">
                  {t("quota", { used: experiences.data.quota.used, limit: experiences.data.quota.limit })}
                </span>
              )}
            </h3>
            {experiences.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
            {experiences.isError && (
              <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                {t("error")}
              </p>
            )}
            {experiences.data && experiences.data.items.length === 0 && (
              <p className="rounded-lg border border-dashed border-gray-300 p-3 text-center text-sm text-gray-500 dark:border-gray-700 dark:text-gray-400">
                {t("experiences.empty")}
              </p>
            )}
            {experiences.data && experiences.data.items.length > 0 && (
              <ul className="flex flex-col gap-2">
                {takeRecent(experiences.data.items).map((entry) => (
                  <li
                    key={entry.id}
                    className="flex items-center justify-between gap-3 rounded-lg border border-gray-200 px-3 py-2 text-sm dark:border-gray-800"
                  >
                    <div className="min-w-0">
                      <Link href={`/companies/${entry.companySlug}?tab=experiences`} className="block truncate font-medium text-gray-900 underline-offset-2 hover:underline dark:text-gray-100">
                        {entry.companyName}
                      </Link>
                      <span className="block truncate text-xs text-gray-500 dark:text-gray-400">
                        {[dateFormat.format(new Date(entry.submittedAt)), entry.outcome ? tOutcome(entry.outcome) : null].filter(Boolean).join(" · ")}
                      </span>
                    </div>
                    <StarRating value={entry.overallRating} label={t("experiences.rating", { value: entry.overallRating })} />
                  </li>
                ))}
              </ul>
            )}
            <div className="mt-1 flex flex-wrap items-center justify-between gap-2">
              <Link href="/my-reviews" className="text-sm text-accent-ink underline-offset-2 hover:underline">
                {t("experiences.all")}
              </Link>
              <Link href="/contribute?tab=experience" className={buttonClassName("outline")}>
                {t("experiences.share")}
              </Link>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}
