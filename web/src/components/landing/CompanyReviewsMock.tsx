"use client";

import { useTranslations } from "next-intl";
import type { CompanyReviewSummary } from "@/types/api";
import { ReviewSummaryPanel } from "@/components/companyReviews/ReviewSummaryPanel";
import { StarRating } from "@/components/companyReviews/StarRating";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";

/**
 * What a company page looks like, with fixed figures: the real aggregate panel (the same component
 * the company page renders) over one review excerpt. The excerpt is markup of its own rather than
 * ReviewCard, because that card always carries its helpful and report controls and a picture for
 * assistive technology must hold nothing focusable. Same company as the extension mock, so the
 * strip tells one story.
 */
const DEMO: CompanyReviewSummary = {
  approvedCount: 12,
  score: 4.1,
  minimumForScore: 3,
  priorWeight: 5,
  averageOverall: 4.1,
  averageManagement: 3.8,
  averageWorkEnvironment: 4.3,
  averageSalaryAndBenefits: 3.5,
  averageCareerAndDevelopment: 4.0,
  distribution: [0, 1, 2, 5, 4],
};

export function CompanyReviewsMock({ className = "" }: { className?: string }) {
  const t = useTranslations("landing.tools.panels.companies.mock");
  const tStatus = useTranslations("employmentStatus");

  return (
    <div className={`relative ${className}`}>
      <SampleDataBadge className="-bottom-2.5 left-3" />
      <div role="img" aria-label={t("ariaLabel")}>
        <div aria-hidden="true" className="flex flex-col gap-3">
          <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">Acme Yazılım A.Ş.</span>
          <ReviewSummaryPanel summary={DEMO} showScoringLink={false} compact />
          <div className="flex flex-col gap-2.5 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
            <div className="flex items-start justify-between gap-3">
              <div className="flex flex-col gap-0.5">
                <span className="font-semibold text-gray-900 dark:text-gray-100">{t("title")}</span>
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  {tStatus("FormerEmployee")} · {t("month")}
                </span>
              </div>
              <span className="flex items-center gap-1.5">
                <StarRating value={5} label="5 / 5" />
                <span className="text-sm font-medium text-gray-900 dark:text-gray-100">5</span>
              </span>
            </div>
            <div className="grid gap-4 text-sm sm:grid-cols-2">
              <div>
                <span className="mb-1 block text-[11px] font-semibold tracking-wider text-good-ink">{t("prosLabel")}</span>
                <span className="text-gray-700 dark:text-gray-300">{t("pros")}</span>
              </div>
              <div>
                <span className="mb-1 block text-[11px] font-semibold tracking-wider text-crit-ink">{t("consLabel")}</span>
                <span className="text-gray-700 dark:text-gray-300">{t("cons")}</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
