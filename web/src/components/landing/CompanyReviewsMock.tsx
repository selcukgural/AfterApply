"use client";

import { useTranslations } from "next-intl";
import type { CompanyReviewSummary } from "@/types/api";
import { ReviewSummaryPanel } from "@/components/companyReviews/ReviewSummaryPanel";
import { StarRating } from "@/components/companyReviews/StarRating";
import { StatementTag } from "@/components/companyReviews/StatementChip";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";

/**
 * What a company page looks like, with fixed figures: the real aggregate panel (the same component
 * the company page renders) over one review excerpt. The excerpt is markup of its own rather than
 * ReviewCard, because that card always carries its helpful and report controls and a picture for
 * assistive technology must hold nothing focusable; its chips carry landing copy rather than
 * catalogue keys for the same bundle-size reason DEMO's top lists are empty. Same company as the
 * extension mock, so the strip tells one story.
 */
const DEMO: CompanyReviewSummary = {
  approvedCount: 12,
  score: 4.1,
  minimumForScore: 3,
  priorWeight: 5,
  averageOverall: 4.1,
  categories: [
    { category: "WorkEnvironment", count: 11, average: 4.3 },
    { category: "Management", count: 9, average: 3.8 },
    { category: "CareerGrowth", count: 8, average: 4.0 },
    { category: "WorkLifeBalance", count: 0, average: null },
    { category: "Pay", count: 10, average: 3.2 },
    { category: "Benefits", count: 0, average: null },
    { category: "RemoteWork", count: 7, average: 4.5 },
    { category: "Tooling", count: 0, average: null },
    { category: "Hiring", count: 0, average: null },
    { category: "Onboarding", count: 0, average: null },
  ],
  distribution: [0, 1, 2, 5, 4],
  // Empty on purpose: the "most picked" lists would pull the statement catalogue's 400 strings
  // into the landing page's bundle for two chips (see messageScopes.ts).
  topLiked: [],
  topImprovable: [],
};

const LIKED = ["liked1", "liked2", "liked3"] as const;
const IMPROVABLE = ["improve1", "improve2"] as const;

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
                <span className="font-semibold text-gray-900 dark:text-gray-100">{tStatus("FormerEmployee")}</span>
                <span className="text-xs text-gray-500 dark:text-gray-400">{t("month")}</span>
              </div>
              <span className="flex items-center gap-1.5">
                <StarRating value={5} label="5 / 5" />
                <span className="text-sm font-medium text-gray-900 dark:text-gray-100">5</span>
              </span>
            </div>
            <div className="grid gap-4 text-sm sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <span className="text-[11px] font-semibold tracking-wider text-good-ink">{t("likedLabel")}</span>
                <span className="flex flex-wrap gap-1.5">
                  {LIKED.map((key) => (
                    <StatementTag key={key} label={t(key)} sentence={t(key)} kind="Liked" />
                  ))}
                </span>
              </div>
              <div className="flex flex-col gap-1.5">
                <span className="text-[11px] font-semibold tracking-wider text-warn-ink">{t("improvableLabel")}</span>
                <span className="flex flex-wrap gap-1.5">
                  {IMPROVABLE.map((key) => (
                    <StatementTag key={key} label={t(key)} sentence={t(key)} kind="Improve" />
                  ))}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
