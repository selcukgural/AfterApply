"use client";

import { useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { CompanyReviewRequest, ResolvedCompany } from "@/types/api";
import { companiesApi } from "@/lib/api/companies";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { ApiError } from "@/lib/api/httpClient";
import { EMPTY_REVIEW_DRAFT } from "@/lib/companyReviews/reviewDraft";
import { Button } from "@/components/ui/Button";
import { Combobox } from "@/components/ui/Combobox";
import { FormField } from "@/components/ui/FormField";
import { CompanyReviewForm } from "@/components/companyReviews/CompanyReviewForm";
import { ReviewGuidelines } from "@/components/companyReviews/ReviewGuidelines";

/**
 * Write a review. Arrives either with `?company=<slug>` (from a company page) or bare (from the
 * menu), in which case the first step is picking the company — the same typeahead the application
 * form uses, plus "use this name" for one that has no page yet.
 */
export default function WriteReviewPage() {
  const t = useTranslations("companyReviews.write");
  const router = useRouter();
  const queryClient = useQueryClient();
  const slug = useSearchParams().get("company");
  const [companyName, setCompanyName] = useState("");
  const [picked, setPicked] = useState<ResolvedCompany | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  const fromSlug = useQuery({
    queryKey: ["companies", "public", slug],
    queryFn: () => companiesApi.getPublic(slug!),
    enabled: !!slug && !picked,
  });

  const resolve = useMutation({
    mutationFn: (name: string) => companiesApi.resolve(name),
    onSuccess: (company) => setPicked(company),
    onError: (err) => setServerError(err instanceof ApiError ? err.message : t("error")),
  });

  const company: ResolvedCompany | null =
    picked ?? (fromSlug.data ? { id: fromSlug.data.id, slug: fromSlug.data.slug, name: fromSlug.data.name } : null);

  const create = useMutation({
    mutationFn: (request: CompanyReviewRequest) => companyReviewsApi.create(company!.id, request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["companyReviews", "mine"] });
      router.push("/my-reviews");
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : t("error")),
  });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
        <div className="flex flex-col gap-4">
          {!company && (
            <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
              {slug && fromSlug.isLoading && <p className="text-sm text-gray-500">{t("loadingCompany")}</p>}
              {slug && fromSlug.isError && <p className="text-sm text-red-600 dark:text-red-400">{t("companyNotFound")}</p>}
              <FormField label={t("companyLabel")} htmlFor="review-company">
                <Combobox
                  id="review-company"
                  value={companyName}
                  onChange={setCompanyName}
                  onSearch={async (q) => (await companiesApi.search(q)).map((c) => ({ id: c.id, label: c.name }))}
                  placeholder={t("companyPlaceholder")}
                  loadingText={t("searching")}
                  emptyText={t("noMatches")}
                />
              </FormField>
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("companyHint")}</p>
              <Button
                type="button"
                disabled={companyName.trim().length === 0 || resolve.isPending}
                onClick={() => resolve.mutate(companyName.trim())}
                className="self-start"
              >
                {resolve.isPending ? t("resolving") : t("useCompany")}
              </Button>
              {serverError && (
                <p role="alert" className="text-sm text-red-600 dark:text-red-400">
                  {serverError}
                </p>
              )}
            </div>
          )}

          {company && (
            <>
              <p className="text-sm text-gray-600 dark:text-gray-400">
                {t("reviewing")}{" "}
                <Link href={`/companies/${company.slug}`} className="font-medium text-gray-900 underline-offset-2 hover:underline dark:text-gray-100">
                  {company.name}
                </Link>
                {!slug && (
                  <>
                    {" · "}
                    <button type="button" className="underline-offset-2 hover:underline" onClick={() => setPicked(null)}>
                      {t("changeCompany")}
                    </button>
                  </>
                )}
              </p>
              <CompanyReviewForm
                companyName={company.name}
                initialDraft={EMPTY_REVIEW_DRAFT}
                submitLabel={t("submit")}
                serverError={serverError}
                onSubmit={async (request) => {
                  setServerError(null);
                  await create.mutateAsync(request).catch(() => undefined);
                }}
              />
            </>
          )}
        </div>
        <ReviewGuidelines />
      </div>
    </div>
  );
}
