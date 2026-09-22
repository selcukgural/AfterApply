"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companiesApi } from "@/lib/api/companies";
import { ApiError } from "@/lib/api/httpClient";
import { useAuth } from "@/lib/auth/AuthContext";
import { formatScore } from "@/lib/companyReviews/score";
import { directoryCountLines, type DirectoryCountKey } from "@/lib/companyReviews/directoryCard";

/** The kind colours, shared with ContributionKindBadge: blue reviews, green salaries, amber experiences. */
const COUNT_DOT: Record<DirectoryCountKey, string> = {
  reviewCount: "bg-accent",
  salaryCount: "bg-emerald-600 dark:bg-emerald-400",
  experienceCount: "bg-amber-600 dark:bg-amber-400",
};
import { Input } from "@/components/ui/Input";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { StarRating } from "@/components/companyReviews/StarRating";

/** The public directory: companies with at least one published contribution — a review, a salary
 *  entry or a candidate experience — most recently contributed-to first, searchable by name. */
export function CompanyDirectory() {
  const t = useTranslations("companies.directory");
  const locale = useLocale();
  const { isAuthenticated } = useAuth();
  const [search, setSearch] = useState("");
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);

  useEffect(() => {
    const handle = setTimeout(() => {
      setQuery(search.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(handle);
  }, [search]);

  const { data, isLoading, error } = useQuery({
    queryKey: ["companies", "public", { query, page }],
    queryFn: () => companiesApi.listPublic(query, page),
  });

  const writeHref = isAuthenticated ? "/contribute?tab=review" : `/login?next=${encodeURIComponent("/contribute?tab=review")}`;

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <div className="flex-1">
          <Input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder={t("searchPlaceholder")}
            aria-label={t("searchLabel")}
          />
        </div>
        <Link href={writeHref} className={buttonClassName("outline", "whitespace-nowrap text-center")}>
          {t("writeCta")}
        </Link>
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error instanceof ApiError && error.status === 429 ? t("tooMany") : t("error")}
        </p>
      )}

      {data && data.items.length === 0 && (
        <div className="rounded-xl border border-dashed border-gray-300 p-8 text-center text-sm text-gray-600 dark:border-gray-700 dark:text-gray-400">
          <p>{query ? t("noMatches", { query }) : t("empty")}</p>
          <p className="mt-2">{t("beFirst")}</p>
        </div>
      )}

      {data && data.items.length > 0 && (
        <ul className="grid gap-3 sm:grid-cols-2">
          {data.items.map((company) => (
            <li key={company.id}>
              <Link
                href={`/companies/${company.slug}`}
                className="flex h-full flex-col gap-2 rounded-xl border border-gray-200 bg-white p-4 transition-colors hover:border-accent/60 dark:border-gray-800 dark:bg-gray-900"
              >
                <span className="font-semibold text-gray-900 dark:text-gray-100">{company.name}</span>
                {/* No score, no line (2026-09-22). A score needs three approved reviews, so most
                    cards had "Henüz puan yok" where the stars go — a directory of eighteen
                    companies reading as seventeen absences, when every one of those cards holds a
                    real contribution and says so on the lines right below. The stars appear the
                    moment there is a score; until then the card leads with what it has. */}
                {company.score !== null && (
                  <span className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                    <StarRating value={company.score} label={formatScore(company.score, locale)} />
                    <span className="font-medium text-gray-900 dark:text-gray-100">{formatScore(company.score, locale)}</span>
                  </span>
                )}
                {/* One line per kind the company has (see directoryCountLines), so a reader sees at
                    a glance what the page holds before opening it. The dot carries the kind's
                    colour — the same three the contribution badges wear on "My contributions". */}
                <ul className="flex flex-col gap-0.5 text-xs text-gray-600 dark:text-gray-400">
                  {directoryCountLines(company).map((line) => (
                    <li key={line.key} className="flex items-center gap-2">
                      <span aria-hidden="true" className={`h-1.5 w-1.5 shrink-0 rounded-full ${COUNT_DOT[line.key]}`} />
                      {t(line.key, { count: line.count })}
                    </li>
                  ))}
                </ul>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {data && (
        <Pagination page={data.page} pageSize={data.pageSize} totalCount={data.totalCount} unit="companies" onPageChange={setPage} />
      )}
    </div>
  );
}
