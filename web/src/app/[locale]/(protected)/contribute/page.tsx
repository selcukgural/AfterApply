"use client";

import { Suspense, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { CompanyReviewRequest, CompanySalaryRequest, ResolvedCompany } from "@/types/api";
import { companiesApi } from "@/lib/api/companies";
import { companyReviewsApi } from "@/lib/api/companyReviews";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { ApiError } from "@/lib/api/httpClient";
import { useClientConfig } from "@/hooks/useClientConfig";
import { EMPTY_REVIEW_DRAFT } from "@/lib/companyReviews/reviewDraft";
import { EMPTY_SALARY_DRAFT } from "@/lib/companySalaries/salaryDraft";
import { contributeHref, nextSideAfterSave, parseContributeTab, type ContributeTab } from "@/lib/contribute/contributeState";
import { Button, buttonClassName } from "@/components/ui/Button";
import { Combobox } from "@/components/ui/Combobox";
import { FormField } from "@/components/ui/FormField";
import { CompanyReviewForm } from "@/components/companyReviews/CompanyReviewForm";
import { ReviewGuidelines } from "@/components/companyReviews/ReviewGuidelines";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";
import { CompanySalaryForm } from "@/components/companySalaries/CompanySalaryForm";
import { SalaryGuidelines } from "@/components/companySalaries/SalaryGuidelines";
import { ContributeSwitch } from "@/components/contribute/ContributeSwitch";
import { ContributionBanner } from "@/components/contribute/ContributionBanner";

/**
 * One page for both things a signed-in person can say about a company (design canvas 2B,
 * 2026-09-16): pick the company once, then switch between the review and the salary form. The
 * side and the company ride in the URL (`?tab=`, `?company=`), so the menu's two links open the
 * right side, a refresh keeps the company, and a company page can link straight in.
 *
 * After a save the page flips to the other side with a thank-you banner (3B) — unless that side
 * has nothing left to offer, in which case it goes to the author's list as it always did.
 */
export default function ContributePage() {
  return (
    <Suspense fallback={null}>
      <ContributeContent />
    </Suspense>
  );
}

function ContributeContent() {
  const t = useTranslations("contribute");
  const tWrite = useTranslations("companyReviews.write");
  const tSalary = useTranslations("companySalaries.write");
  const router = useRouter();
  const queryClient = useQueryClient();
  const { config } = useClientConfig();
  const params = useSearchParams();
  const tab = parseContributeTab(params.get("tab"));
  const slug = params.get("company");

  const [companyName, setCompanyName] = useState("");
  const [picked, setPicked] = useState<ResolvedCompany | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [banner, setBanner] = useState<ContributeTab | null>(null);

  const fromSlug = useQuery({
    queryKey: ["companies", "public", slug],
    queryFn: () => companiesApi.getPublic(slug!),
    enabled: !!slug && !picked,
  });

  const resolve = useMutation({
    mutationFn: (name: string) => companiesApi.resolve(name),
    onSuccess: (company) => {
      setPicked(company);
      // The URL is the record of what was picked: a refresh or the back button keeps it.
      router.replace(contributeHref(tab, company.slug));
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : t("error")),
  });

  const company: ResolvedCompany | null =
    picked ?? (fromSlug.data ? { id: fromSlug.data.id, slug: fromSlug.data.slug, name: fromSlug.data.name } : null);

  const reviewViewer = useQuery({
    queryKey: ["companies", company?.id, "viewer"],
    queryFn: () => companyReviewsApi.viewerState(company!.id),
    enabled: !!company,
  });
  const salaryViewer = useQuery({
    queryKey: ["companies", company?.id, "salaryViewer"],
    queryFn: () => companySalariesApi.viewerState(company!.id),
    enabled: !!company && config.companySalaries?.enabled !== false,
  });

  const goTo = (next: ContributeTab) => {
    setServerError(null);
    router.replace(contributeHref(next, company?.slug));
  };

  const invalidate = () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["companyReviews", "mine"] }),
      queryClient.invalidateQueries({ queryKey: ["companySalaries", "mine"] }),
      queryClient.invalidateQueries({ queryKey: ["companies", "public", company!.slug] }),
      queryClient.invalidateQueries({ queryKey: ["companies", company!.id, "viewer"] }),
      queryClient.invalidateQueries({ queryKey: ["companies", company!.id, "salaryViewer"] }),
      queryClient.invalidateQueries({ queryKey: ["companies", company!.id, "salaries"] }),
    ]);

  // What the other side can still offer, after this save is counted. The viewer queries are
  // refetched by the invalidation above, but the decision is made from what we know right now.
  const afterSave = (saved: ContributeTab) => {
    const reviewQuota = reviewViewer.data?.quota;
    const salaryQuota = salaryViewer.data?.quota;
    const salaryUsed = (salaryQuota?.used ?? 0) + (saved === "salary" ? 1 : 0);
    const next = nextSideAfterSave(saved, {
      ownReview: reviewViewer.data?.ownReview !== null && reviewViewer.data?.ownReview !== undefined,
      ownSalaryCount: (salaryViewer.data?.ownEntries.length ?? 0) + (saved === "salary" ? 1 : 0),
      reviewQuotaLeft: reviewQuota ? reviewQuota.limit - reviewQuota.used : 0,
      salaryQuotaLeft: salaryQuota ? salaryQuota.limit - salaryUsed : 0,
      reviewsEnabled: config.companyReviews?.enabled !== false,
      salariesEnabled: config.companySalaries?.enabled === true,
    });
    if (next === null) {
      router.push(saved === "salary" ? "/my-salaries" : "/my-reviews");
      return;
    }
    setBanner(saved);
    goTo(next);
    if (typeof window !== "undefined") window.scrollTo({ top: 0 });
  };

  const createReview = useMutation({
    mutationFn: (request: CompanyReviewRequest) => companyReviewsApi.create(company!.id, request),
    onSuccess: async () => {
      await invalidate();
      afterSave("review");
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : tWrite("error")),
  });

  const createSalary = useMutation({
    mutationFn: (request: CompanySalaryRequest) => companySalariesApi.create(company!.id, request),
    onSuccess: async () => {
      await invalidate();
      afterSave("salary");
    },
    onError: (err) => setServerError(err instanceof ApiError ? err.message : tSalary("error")),
  });

  const ownReview = reviewViewer.data?.ownReview ?? null;
  const ownSalaries = salaryViewer.data?.ownEntries ?? [];
  const salaryQuota = salaryViewer.data?.quota;
  const salariesOn = config.companySalaries?.enabled === true;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>

      {banner && company && <ContributionBanner saved={banner} company={company.name} />}

      {!company && (
        <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
          {slug && fromSlug.isLoading && <p className="text-sm text-gray-500">{tWrite("loadingCompany")}</p>}
          {slug && fromSlug.isError && <p className="text-sm text-red-600 dark:text-red-400">{tWrite("companyNotFound")}</p>}
          <FormField label={tWrite("companyLabel")} htmlFor="contribute-company">
            <Combobox
              id="contribute-company"
              value={companyName}
              onChange={setCompanyName}
              onSearch={async (q) => (await companiesApi.search(q)).map((c) => ({ id: c.id, label: c.name }))}
              placeholder={tWrite("companyPlaceholder")}
              loadingText={tWrite("searching")}
              emptyText={tWrite("noMatches")}
            />
          </FormField>
          <p className="text-xs text-gray-500 dark:text-gray-400">{tWrite("companyHint")}</p>
          <Button
            type="button"
            disabled={companyName.trim().length === 0 || resolve.isPending}
            onClick={() => resolve.mutate(companyName.trim())}
            className="self-start"
          >
            {resolve.isPending ? tWrite("resolving") : tWrite("useCompany")}
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
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white px-5 py-3 dark:border-gray-800 dark:bg-gray-900">
            <div className="flex flex-wrap items-center gap-3 text-sm">
              <span className="font-medium text-gray-700 dark:text-gray-300">{t("companyLabel")}</span>
              <span className="inline-flex items-center gap-2 rounded-md bg-muted-wash px-2.5 py-1.5 font-medium text-gray-900 dark:text-gray-100">
                <Link href={`/companies/${company.slug}`} className="underline-offset-2 hover:underline">
                  {company.name}
                </Link>
                <button
                  type="button"
                  className="font-normal text-gray-600 underline-offset-2 hover:underline dark:text-gray-400"
                  onClick={() => {
                    setPicked(null);
                    setBanner(null);
                    router.replace(contributeHref(tab));
                  }}
                >
                  {tWrite("changeCompany")}
                </button>
              </span>
              {reviewViewer.data && (
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  {salariesOn
                    ? t("status", { reviews: ownReview ? 1 : 0, salaries: ownSalaries.length })
                    : t("statusReviewsOnly", { reviews: ownReview ? 1 : 0 })}
                </span>
              )}
            </div>
            {salariesOn && <ContributeSwitch value={tab} onChange={goTo} />}
          </div>

          <div className="grid gap-6 lg:grid-cols-[1fr_20rem]">
            {tab === "salary" && salariesOn ? (
              <>
                <div className="flex flex-col gap-4">
                  {salaryQuota && salaryQuota.used >= salaryQuota.limit ? (
                    <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-5 text-sm dark:border-gray-800 dark:bg-gray-900/60">
                      <p className="text-gray-700 dark:text-gray-300">{t("salaryQuotaFull", { limit: salaryQuota.limit })}</p>
                      <Link href="/my-salaries" className={`${buttonClassName("outline")} self-start`}>
                        {t("goToMySalaries")}
                      </Link>
                    </div>
                  ) : (
                    <CompanySalaryForm
                      key={company.id}
                      companyName={company.name}
                      initialDraft={EMPTY_SALARY_DRAFT}
                      submitLabel={tSalary("submit")}
                      serverError={serverError}
                      quota={salaryQuota}
                      onSubmit={async (request) => {
                        setServerError(null);
                        await createSalary.mutateAsync(request).catch(() => undefined);
                      }}
                    />
                  )}
                </div>
                <SalaryGuidelines />
              </>
            ) : (
              <>
                <div className="flex flex-col gap-4">
                  {ownReview ? (
                    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-accent/40 bg-accent-wash p-4 text-sm">
                      <div className="flex flex-col gap-1">
                        <span className="font-medium text-gray-900 dark:text-gray-100">{t("alreadyReviewed", { company: company.name })}</span>
                        <span className="flex items-center gap-2 text-gray-700 dark:text-gray-300">
                          <ReviewStatusBadge status={ownReview.status} />
                          <span>{t("alreadyReviewedHint")}</span>
                        </span>
                      </div>
                      <Link href={`/my-reviews/${ownReview.id}/edit`} className={buttonClassName("outline")}>
                        {t("editReview")}
                      </Link>
                    </div>
                  ) : (
                    <CompanyReviewForm
                      key={company.id}
                      companyName={company.name}
                      initialDraft={EMPTY_REVIEW_DRAFT}
                      submitLabel={tWrite("submit")}
                      serverError={serverError}
                      onSubmit={async (request) => {
                        setServerError(null);
                        await createReview.mutateAsync(request).catch(() => undefined);
                      }}
                    />
                  )}
                </div>
                <ReviewGuidelines />
              </>
            )}
          </div>
        </>
      )}
    </div>
  );
}
