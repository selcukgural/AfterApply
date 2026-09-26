"use client";

import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { MyCompanyReview } from "@/types/api";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { ApiError } from "@/lib/api/httpClient";
import { guidePath } from "@/lib/guide/guideLinks";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { StarRating } from "@/components/companyReviews/StarRating";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";
import { ReviewPicks } from "@/components/companyReviews/ReviewPicks";
import { ContributionKindBadge } from "@/components/contributions/ContributionKindBadge";
import { MyProofLine } from "@/components/contributions/ProofLabel";

interface MyReviewCardProps {
  review: MyCompanyReview;
  /** Called once the row is gone on the server; the list decides what to refetch. */
  onDeleted: () => Promise<void>;
  /** Whether the company page shows the "tracked application" label on this row. */
  backed: boolean;
}

/** The author's own review on the merged contributions list: status, picks or legacy text, and
 *  the edit/delete pair. Deleting is confirmed here; the list only learns that it happened. */
export function MyReviewCard({ review, backed, onDeleted }: MyReviewCardProps) {
  const t = useTranslations("companyReviews.mine");
  const locale = useLocale();
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const remove = useMutation({
    mutationFn: () => companyReviewsApi.remove(review.id),
    onSuccess: async () => {
      setConfirming(false);
      await onDeleted();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <ContributionKindBadge kind="Review" />
            <Link href={`/companies/${review.companySlug}`} className="text-sm text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
              {review.companyName}
            </Link>
          </span>
          {review.format === "Legacy" ? (
            <span className="font-semibold text-gray-900 dark:text-gray-100">{review.title}</span>
          ) : (
            <span className="text-sm text-gray-700 dark:text-gray-300">
              {t("structuredSummary", { count: review.likedStatements.length + review.improvableStatements.length })}
            </span>
          )}
          <span className="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
            <ReviewStatusBadge status={review.status} />
            {review.format === "Legacy" ? (
              <span className="rounded-full bg-muted-wash px-2 py-0.5 text-[11px] font-medium text-muted-ink">{t("legacyTag")}</span>
            ) : null}
            <span>{new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(new Date(review.submittedAt))}</span>
          </span>
        </div>
        <StarRating value={review.overallRating} label={String(review.overallRating)} size="lg" />
      </div>

      {review.format === "Legacy" ? (
        <div className="flex flex-col gap-2 text-sm text-gray-700 dark:text-gray-300">
          {review.pros ? <p className="whitespace-pre-wrap">{review.pros}</p> : null}
          {review.cons ? <p className="whitespace-pre-wrap">{review.cons}</p> : null}
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("legacyEditHint")}</p>
        </div>
      ) : (
        <ReviewPicks liked={review.likedStatements} improvable={review.improvableStatements} />
      )}

      {review.status === "Rejected" && review.rejectionReason && (
        <p className="rounded-lg bg-red-50 p-3 text-sm text-red-800 dark:bg-red-900/30 dark:text-red-200">
          <span className="font-medium">{t("rejectedTitle")}</span> {review.rejectionReason}
          <br />
          <span className="text-xs">
            {t("rejectedHint")}{" "}
            <Link href={guidePath("writing-a-fair-review", locale)} className="font-medium underline underline-offset-2">
              {t("rejectedGuideLink")}
            </Link>
          </span>
        </p>
      )}
      {review.status === "Pending" && <p className="text-xs text-gray-500 dark:text-gray-400">{t("pendingHint")}</p>}

      <MyProofLine kind="accepted" backed={backed} />

      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      <div className="flex gap-2">
        <Link href={`/my-reviews/${review.id}/edit`} className={buttonClassName("secondary")}>
          {t("edit")}
        </Link>
        <Button variant="danger" onClick={() => setConfirming(true)}>
          {t("delete")}
        </Button>
      </div>

      {confirming && (
        <Modal
          title={t("deleteTitle")}
          onClose={() => setConfirming(false)}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setConfirming(false)} disabled={remove.isPending}>
                {t("cancel")}
              </Button>
              <Button variant="danger" onClick={() => remove.mutate()} disabled={remove.isPending}>
                {t("confirmDelete")}
              </Button>
            </>
          }
        >
          <h2 className="text-lg font-semibold">{t("deleteTitle")}</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("deleteBody")}</p>
        </Modal>
      )}
    </li>
  );
}
