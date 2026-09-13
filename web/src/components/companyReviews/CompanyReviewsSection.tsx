"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CompanyPublicResponse, CompanyReviewPublic, PagedResult, PublicReviewSort, ReviewReportReason } from "@/types/api";
import { companiesApi } from "@/lib/api/companies";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { ApiError } from "@/lib/api/httpClient";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { ReviewCard } from "@/components/companyReviews/ReviewCard";
import { ReportReviewDialog } from "@/components/companyReviews/ReportReviewDialog";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";

interface CompanyReviewsSectionProps {
  company: CompanyPublicResponse;
  /** The first page, newest first, as rendered on the server — so the HTML carries the reviews. */
  initialReviews: PagedResult<CompanyReviewPublic>;
}

/**
 * The interactive half of a company page. The server rendered the first page of reviews; this
 * takes over for paging, sorting, and everything that needs a signed-in reader — the "write a
 * review" call to action, the reader's own review (any status), helpful marks and reports.
 */
export function CompanyReviewsSection({ company, initialReviews }: CompanyReviewsSectionProps) {
  const t = useTranslations("companies.reviews");
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<PublicReviewSort>("Newest");
  const [reporting, setReporting] = useState<string | null>(null);
  const [reportError, setReportError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const listQuery = useQuery({
    queryKey: ["companies", "public", company.slug, "reviews", { page, sort }],
    queryFn: () => companiesApi.listPublicReviews(company.slug, page, sort),
    initialData: page === 1 && sort === "Newest" ? initialReviews : undefined,
  });

  const viewerQuery = useQuery({
    queryKey: ["companies", company.id, "viewer"],
    queryFn: () => companyReviewsApi.viewerState(company.id),
    enabled: isAuthenticated,
  });

  const helpful = useMutation({
    mutationFn: (reviewId: string) => companyReviewsApi.toggleHelpful(reviewId),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["companies", "public", company.slug, "reviews"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", company.id, "viewer"] }),
      ]);
    },
    onError: (error) => setActionError(error instanceof ApiError ? error.message : t("actionError")),
  });

  const report = useMutation({
    mutationFn: ({ reviewId, reason, note }: { reviewId: string; reason: ReviewReportReason; note: string | null }) =>
      companyReviewsApi.report(reviewId, { reason, note }),
    onSuccess: () => {
      setReporting(null);
      setReportError(null);
      setActionError(null);
    },
    onError: (error) => setReportError(error instanceof ApiError ? error.message : t("actionError")),
  });

  const writePath = `/my-reviews/write?company=${company.slug}`;
  const signInHref = `/login?next=${encodeURIComponent(writePath)}`;
  const ownReview = viewerQuery.data?.ownReview ?? null;
  const marked = new Set(viewerQuery.data?.helpfulMarkedReviewIds ?? []);
  const reviews = listQuery.data;

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {t("heading", { count: company.summary.approvedCount })}
        </h2>
        <div className="flex items-center gap-2">
          <label className="text-xs text-gray-500 dark:text-gray-400" htmlFor="review-sort">
            {t("sortLabel")}
          </label>
          <select
            id="review-sort"
            value={sort}
            onChange={(e) => {
              setSort(e.target.value as PublicReviewSort);
              setPage(1);
            }}
            className="rounded-md border border-gray-300 bg-white px-2 py-1 text-xs text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
          >
            <option value="Newest">{t("sortNewest")}</option>
            <option value="MostHelpful">{t("sortMostHelpful")}</option>
          </select>
        </div>
      </div>

      {ownReview ? (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-accent/40 bg-accent-wash p-4 text-sm">
          <div className="flex flex-col gap-1">
            <span className="font-medium text-gray-900 dark:text-gray-100">{t("yourReview")}</span>
            <span className="flex items-center gap-2 text-gray-700 dark:text-gray-300">
              <ReviewStatusBadge status={ownReview.status} />
              <span>{ownReview.title}</span>
            </span>
            {ownReview.status === "Rejected" && ownReview.rejectionReason && (
              <span className="text-xs text-red-700 dark:text-red-400">{t("rejectedReason", { reason: ownReview.rejectionReason })}</span>
            )}
          </div>
          <Link href={`/my-reviews/${ownReview.id}/edit`} className={buttonClassName("outline")}>
            {t("editYourReview")}
          </Link>
        </div>
      ) : (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-gray-50 p-4 text-sm dark:border-gray-800 dark:bg-gray-900/60">
          <span className="text-gray-700 dark:text-gray-300">{t("cta")}</span>
          {isAuthenticated ? (
            <Link href={writePath} className={buttonClassName("primary")}>
              {t("writeReview")}
            </Link>
          ) : (
            <a href={signInHref} className={buttonClassName("primary")}>
              {t("signInToWrite")}
            </a>
          )}
        </div>
      )}

      {actionError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {actionError}
        </p>
      )}

      {reviews && reviews.items.length === 0 && (
        <p className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          {t("empty")}
        </p>
      )}

      {reviews && reviews.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {reviews.items.map((review) => (
            <li key={review.id}>
              <ReviewCard
                review={review}
                helpfulMarked={marked.has(review.id)}
                busy={helpful.isPending}
                signInHref={signInHref}
                onToggleHelpful={isAuthenticated ? () => helpful.mutate(review.id) : undefined}
                onReport={isAuthenticated ? () => setReporting(review.id) : undefined}
              />
            </li>
          ))}
        </ul>
      )}

      {reviews && (
        <Pagination page={reviews.page} pageSize={reviews.pageSize} totalCount={reviews.totalCount} unit="companies" onPageChange={setPage} />
      )}

      {reporting && (
        <ReportReviewDialog
          onClose={() => {
            setReporting(null);
            setReportError(null);
          }}
          error={reportError}
          onSubmit={(reason, note) => report.mutateAsync({ reviewId: reporting, reason, note }).then(() => undefined, () => undefined)}
        />
      )}
    </section>
  );
}
