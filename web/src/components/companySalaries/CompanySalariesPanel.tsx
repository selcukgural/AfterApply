"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CompanyPublicResponse, CompanySalaryPublic } from "@/types/api";
import { companySalariesApi } from "@/lib/api/companySalaries";
import { useAuth } from "@/lib/auth/AuthContext";
import { contributeHref } from "@/lib/contribute/contributeState";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { SalaryRow } from "@/components/companySalaries/SalaryRow";
import { SalaryStatsStrip } from "@/components/companySalaries/SalaryStatsStrip";

// Two rows behind the sign-in card, so a visitor sees the shape of what they would get. Sample
// figures, not real ones: the real list is never in a public response.
const SAMPLE_ROWS: CompanySalaryPublic[] = [
  {
    id: "sample-1",
    occupation: { id: "sample-1", code: "EK-0001", nameTr: "Backend Developer", nameEn: "Backend Developer" },
    experienceBand: "FiveToNine",
    employmentType: "FullTime",
    employmentStatus: "CurrentEmployee",
    monthlyNetAmount: 95_000,
    currency: "TRY",
    annualBonusAmount: 120_000,
    submittedMonth: "2026-09",
    periodStartYear: 2024,
    periodEndYear: null,
    isCurrentPeriod: true,
  },
  {
    id: "sample-2",
    occupation: { id: "sample-2", code: "EK-0002", nameTr: "Frontend Developer", nameEn: "Frontend Developer" },
    experienceBand: "TwoToFour",
    employmentType: "FullTime",
    employmentStatus: "FormerEmployee",
    monthlyNetAmount: 62_000,
    currency: "TRY",
    annualBonusAmount: null,
    submittedMonth: "2026-08",
    periodStartYear: 2023,
    periodEndYear: 2025,
    isCurrentPeriod: true,
  },
];

/**
 * The salary tab of a company page. The page itself is public and server-rendered; this panel is
 * the part that needs an account — the list is fetched only for a signed-in reader (the API
 * answers 401 otherwise), and a visitor sees blurred sample rows behind a sign-in card that
 * brings them back to this tab.
 */
export function CompanySalariesPanel({ company }: { company: CompanyPublicResponse }) {
  const t = useTranslations("companySalaries.panel");
  const { isAuthenticated } = useAuth();
  const [page, setPage] = useState(1);

  const listQuery = useQuery({
    queryKey: ["companies", company.id, "salaries", { page }],
    queryFn: () => companySalariesApi.list(company.id, page),
    enabled: isAuthenticated,
  });
  const viewerQuery = useQuery({
    queryKey: ["companies", company.id, "salaryViewer"],
    queryFn: () => companySalariesApi.viewerState(company.id),
    enabled: isAuthenticated,
  });

  const shareHref = contributeHref("salary", company.slug);
  const returnTo = `/companies/${company.slug}?tab=salaries`;
  const signInHref = `/login?next=${encodeURIComponent(returnTo)}`;
  const count = company.salaryCount ?? 0;

  if (!isAuthenticated) {
    return (
      <section className="relative flex min-h-[26rem] flex-col gap-3" aria-labelledby="salaries-locked-title">
        <div className="flex flex-col gap-3 blur-sm opacity-60 select-none" aria-hidden="true">
          {SAMPLE_ROWS.map((row) => (
            <SalaryRow key={row.id} entry={row} />
          ))}
        </div>
        <div className="absolute inset-0 flex items-center justify-center p-4">
          <div className="flex max-w-md flex-col items-center gap-3 rounded-xl border border-gray-200 bg-white p-6 text-center shadow-lg dark:border-gray-800 dark:bg-gray-900">
            <span className="inline-flex h-11 w-11 items-center justify-center rounded-full bg-accent-wash text-accent-ink">
              <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth={1.8} aria-hidden="true">
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M16.5 10.5V6.75a4.5 4.5 0 10-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 002.25-2.25v-6.75a2.25 2.25 0 00-2.25-2.25H6.75a2.25 2.25 0 00-2.25 2.25v6.75a2.25 2.25 0 002.25 2.25z"
                />
              </svg>
            </span>
            <h2 id="salaries-locked-title" className="text-base font-semibold text-gray-900 dark:text-gray-100">
              {t("lockedTitle")}
            </h2>
            <p className="text-sm text-gray-600 dark:text-gray-400">{t("lockedBody", { count, company: company.name })}</p>
            <div className="flex gap-2">
              <a href={signInHref} className={buttonClassName("primary")}>
                {t("signIn")}
              </a>
              <Link href="/register" className={buttonClassName("outline")}>
                {t("register")}
              </Link>
            </div>
          </div>
        </div>
      </section>
    );
  }

  const list = listQuery.data;
  const own = viewerQuery.data;
  const quotaLeft = own ? Math.max(0, own.quota.limit - own.quota.used) : 0;
  const pendingStats = list?.stats.filter((s) => s.medianMonthlyNet === null) ?? [];
  // The server puts current rows first, so the "previous periods" line is drawn once, where the
  // first previous row sits — on whichever page that happens to be.
  const firstPreviousIndex = list?.items.findIndex((entry) => !entry.isCurrentPeriod) ?? -1;

  return (
    <section className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("heading", { count: list?.total ?? count })}</h2>
        {own && quotaLeft > 0 && (
          <Link href={shareHref} className={buttonClassName("outline")}>
            {t("share")}
          </Link>
        )}
      </div>

      <p className="text-xs text-gray-600 dark:text-gray-400">{t("readingNote")}</p>

      {own && own.ownEntries.length > 0 && (
        <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-accent/40 bg-accent-wash p-4 text-sm">
          <span className="text-gray-900 dark:text-gray-100">{t("yours", { count: own.ownEntries.length })}</span>
          <Link href="/my-reviews" className={buttonClassName("outline")}>
            {t("manageYours")}
          </Link>
        </div>
      )}

      {listQuery.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {listQuery.isError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {t("loadError")}
        </p>
      )}

      {list && <SalaryStatsStrip stats={list.stats} windowYears={list.currentWindowYears} />}
      {list && pendingStats.length > 0 && list.items.length > 0 && (
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {t("statsPending", { minimum: list.minimumForStats })}
        </p>
      )}

      {list && list.items.length === 0 && (
        <div className="flex flex-col items-start gap-3 rounded-xl border border-dashed border-gray-300 p-8 text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{t("empty")}</p>
          {quotaLeft > 0 && (
            <Link href={shareHref} className={buttonClassName("primary")}>
              {t("emptyCta")}
            </Link>
          )}
        </div>
      )}

      {list && list.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {list.items.map((entry, index) => (
            <li key={entry.id} className="flex flex-col gap-3">
              {index === firstPreviousIndex && (
                <div className="flex items-center gap-3 pt-1 text-xs text-gray-500 dark:text-gray-400" role="separator">
                  <span className="h-px flex-1 bg-gray-200 dark:bg-gray-800" aria-hidden="true" />
                  <span>{t("previousPeriods", { count: list.previousPeriodTotal })}</span>
                  <span className="h-px flex-1 bg-gray-200 dark:bg-gray-800" aria-hidden="true" />
                </div>
              )}
              <SalaryRow entry={entry} />
            </li>
          ))}
        </ul>
      )}

      {list && <Pagination page={list.page} pageSize={list.pageSize} totalCount={list.total} unit="salaries" onPageChange={setPage} />}
    </section>
  );
}
