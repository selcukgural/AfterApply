"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { companiesApi } from "@/lib/api/companies";
import { ApiError } from "@/lib/api/httpClient";
import { useAuth } from "@/lib/auth/AuthContext";
import { formatScore } from "@/lib/companyReviews/score";
import { Input } from "@/components/ui/Input";
import { buttonClassName } from "@/components/ui/Button";
import { Pagination } from "@/components/applications/Pagination";
import { StarRating } from "@/components/companyReviews/StarRating";

/** The public directory: companies with at least one published review, searchable by name. */
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

  const writeHref = isAuthenticated ? "/my-reviews/write" : `/login?next=${encodeURIComponent("/my-reviews/write")}`;

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
                <span className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                  {company.score !== null ? (
                    <>
                      <StarRating value={company.score} label={formatScore(company.score, locale)} />
                      <span className="font-medium text-gray-900 dark:text-gray-100">{formatScore(company.score, locale)}</span>
                    </>
                  ) : (
                    <span>{t("noScore")}</span>
                  )}
                  <span>· {t("reviewCount", { count: company.approvedCount })}</span>
                </span>
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
