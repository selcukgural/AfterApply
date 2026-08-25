"use client";

import { useCallback } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { ApplicationListSortBy, ApplicationStatus, SortDirection } from "@/types/api";
import { applicationsApi } from "@/lib/api/applications";
import { ApplicationFilters } from "@/components/applications/ApplicationFilters";
import { ApplicationTable } from "@/components/applications/ApplicationTable";
import { Pagination } from "@/components/applications/Pagination";
import { Button } from "@/components/ui/Button";

const PAGE_SIZE = 20;

export default function ApplicationsListPage() {
  const t = useTranslations("applications.list");
  const tCommon = useTranslations("common");
  const router = useRouter();
  const searchParams = useSearchParams();

  const page = Number(searchParams.get("page") ?? "1");
  const search = searchParams.get("search") ?? "";
  const status = (searchParams.get("status") as ApplicationStatus | null) ?? "";
  const sortBy = (searchParams.get("sortBy") as ApplicationListSortBy | null) ?? "AppliedAt";
  const sortDirection = (searchParams.get("sortDirection") as SortDirection | null) ?? "Descending";

  const updateParams = useCallback(
    (updates: Record<string, string | number | null>) => {
      const params = new URLSearchParams(searchParams.toString());
      for (const [key, value] of Object.entries(updates)) {
        if (value === null || value === "") {
          params.delete(key);
        } else {
          params.set(key, String(value));
        }
      }
      // Any filter/sort change resets pagination back to page 1.
      if (!("page" in updates)) {
        params.delete("page");
      }
      router.push(`/applications?${params.toString()}`);
    },
    [router, searchParams],
  );

  const { data, isLoading } = useQuery({
    queryKey: ["applications", "list", { page, search, status, sortBy, sortDirection }],
    queryFn: () =>
      applicationsApi.getAll({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        status: status || undefined,
        sortBy,
        sortDirection,
      }),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <Link href="/applications/new">
          <Button>{t("newApplication")}</Button>
        </Link>
      </div>

      <ApplicationFilters
        search={search}
        status={status}
        sortBy={sortBy}
        sortDirection={sortDirection}
        onSearchChange={(value) => updateParams({ search: value })}
        onStatusChange={(value) => updateParams({ status: value })}
        onSortByChange={(value) => updateParams({ sortBy: value })}
        onSortDirectionChange={(value) => updateParams({ sortDirection: value })}
      />

      {isLoading || !data ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : (
        <>
          <ApplicationTable items={data.items} />
          <Pagination
            page={data.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            onPageChange={(newPage) => updateParams({ page: newPage })}
          />
        </>
      )}
    </div>
  );
}
