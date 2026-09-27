"use client";

import { use, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSwitchedOff } from "@/hooks/useSwitchedOff";
import { FeatureOffNotice } from "@/components/ui/FeatureOffNotice";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { CompanyReviewRequest } from "@/types/api";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { ApiError } from "@/lib/api/httpClient";
import { draftFromReview } from "@/lib/companyReviews/reviewDraft";
import { CompanyReviewForm } from "@/components/companyReviews/CompanyReviewForm";
import { ReviewGuidelines } from "@/components/companyReviews/ReviewGuidelines";

export default function EditReviewPage({ params }: PageProps<"/[locale]/my-reviews/[id]/edit">) {
  const { id } = use(params);
  const t = useTranslations("companyReviews.edit");
  const router = useRouter();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  // No single-review endpoint: the author's list is at most their quota long, so it is the lookup.
  const companiesOff = useSwitchedOff().companies;
  const tOff = useTranslations("companyReviews.off");
  const { data, isLoading } = useQuery({ queryKey: ["companyReviews", "mine"], queryFn: companyReviewsApi.listMine, enabled: !companiesOff });
  const review = data?.items.find((item) => item.id === id) ?? null;

  const update = useMutation({
    mutationFn: (request: CompanyReviewRequest) => companyReviewsApi.update(id, request),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["contributions", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companyReviews", "mine"] }),
        queryClient.invalidateQueries({ queryKey: ["companies", "public", review!.companySlug] }),
        queryClient.invalidateQueries({ queryKey: ["companies", review!.companyId, "viewer"] }),
      ]);
      router.push("/my-reviews");
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : t("error")),
  });

  // Switched off at runtime (the CompanyReviews flag): every request this page makes answers 404,
  // so it says the feature is off instead of showing an error (DECISIONS.md 2026-09-27).
  if (companiesOff) {
    return <FeatureOffNotice title={tOff("title")} body={tOff("body")} backLabel={tOff("back")} />;
  }

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {data && !review && (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("notFound")}{" "}
          <Link href="/my-reviews" className="text-accent-ink underline-offset-2 hover:underline">
            {t("backToMine")}
          </Link>
        </p>
      )}

      {review && (
        <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
          <CompanyReviewForm
            companyName={review.companyName}
            initialDraft={draftFromReview(review)}
            submitLabel={t("submit")}
            serverError={serverError}
            legacyNotice={review.format === "Legacy" ? t("legacyBanner") : undefined}
            onSubmit={async (request) => {
              setServerError(null);
              await update.mutateAsync(request).catch(() => undefined);
            }}
          />
          <ReviewGuidelines />
        </div>
      )}
    </div>
  );
}
