"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { ApiError } from "@/lib/api/httpClient";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { StarRating } from "@/components/companyReviews/StarRating";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";
import { ReviewPicks } from "@/components/companyReviews/ReviewPicks";
import { guidePath } from "@/lib/guide/articles";

export default function MyReviewsPage() {
  const t = useTranslations("companyReviews.mine");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [deleting, setDeleting] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const { data, isLoading } = useQuery({ queryKey: ["companyReviews", "mine"], queryFn: companyReviewsApi.listMine });

  const remove = useMutation({
    mutationFn: (id: string) => companyReviewsApi.remove(id),
    onSuccess: async () => {
      setDeleting(null);
      await queryClient.invalidateQueries({ queryKey: ["companyReviews", "mine"] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  const quotaLeft = data ? Math.max(0, data.quota.limit - data.quota.used) : 0;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
          <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
          {data && (
            <p className="mt-1 text-xs text-gray-500 dark:text-gray-400">
              {t("quota", { used: data.quota.used, limit: data.quota.limit })}
            </p>
          )}
        </div>
        {quotaLeft > 0 && (
          <Link href="/contribute?tab=review" className={buttonClassName("primary")}>
            {t("write")}
          </Link>
        )}
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}

      {data && data.items.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{t("empty")}</p>
          <Link href="/companies" className="mt-3 inline-block text-accent-ink underline-offset-2 hover:underline">
            {t("browse")}
          </Link>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {data.items.map((review) => (
            <li key={review.id} className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="flex flex-col gap-1">
                  <Link href={`/companies/${review.companySlug}`} className="text-sm text-gray-500 underline-offset-2 hover:underline dark:text-gray-400">
                    {review.companyName}
                  </Link>
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

              <div className="flex gap-2">
                <Link href={`/my-reviews/${review.id}/edit`} className={buttonClassName("secondary")}>
                  {t("edit")}
                </Link>
                <Button variant="danger" onClick={() => setDeleting(review.id)}>
                  {t("delete")}
                </Button>
              </div>
            </li>
          ))}
        </ul>
      )}

      {deleting && (
        <Modal
          title={t("deleteTitle")}
          onClose={() => setDeleting(null)}
          busy={remove.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setDeleting(null)} disabled={remove.isPending}>
                {t("cancel")}
              </Button>
              <Button variant="danger" onClick={() => remove.mutate(deleting)} disabled={remove.isPending}>
                {t("confirmDelete")}
              </Button>
            </>
          }
        >
          <h2 className="text-lg font-semibold">{t("deleteTitle")}</h2>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("deleteBody")}</p>
        </Modal>
      )}
    </div>
  );
}
