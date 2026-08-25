"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationListSortBy, ApplicationStatus, SortDirection } from "@/types/api";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";

interface ApplicationFiltersProps {
  search: string;
  status: ApplicationStatus | "";
  sortBy: ApplicationListSortBy;
  sortDirection: SortDirection;
  onSearchChange: (search: string) => void;
  onStatusChange: (status: ApplicationStatus | "") => void;
  onSortByChange: (sortBy: ApplicationListSortBy) => void;
  onSortDirectionChange: (direction: SortDirection) => void;
}

export function ApplicationFilters({
  search,
  status,
  sortBy,
  sortDirection,
  onSearchChange,
  onStatusChange,
  onSortByChange,
  onSortDirectionChange,
}: ApplicationFiltersProps) {
  const t = useTranslations("applications.filters");
  const tStatus = useTranslations("status");
  const SORT_OPTIONS: { value: ApplicationListSortBy; label: string }[] = [
    { value: "AppliedAt", label: t("sortAppliedAt") },
    { value: "UpdatedAt", label: t("sortUpdatedAt") },
    { value: "CompanyName", label: t("sortCompany") },
    { value: "JobTitle", label: t("sortTitle") },
    { value: "Status", label: t("sortStatus") },
  ];

  const [searchInput, setSearchInput] = useState(search);
  // Adjust state during render (React-documented pattern, not an effect) to
  // reset the local input when the URL's search param changes externally
  // (e.g. browser back/forward) without a setState-in-effect cascade.
  const [prevSearchProp, setPrevSearchProp] = useState(search);
  if (search !== prevSearchProp) {
    setPrevSearchProp(search);
    setSearchInput(search);
  }

  useEffect(() => {
    const timeout = setTimeout(() => {
      if (searchInput !== search) {
        onSearchChange(searchInput);
      }
    }, 300);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  return (
    <div className="flex flex-wrap items-end gap-3">
      <div className="min-w-48 flex-1">
        <Input
          placeholder={t("searchPlaceholder")}
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
      </div>
      <Select
        value={status}
        onChange={(e) => onStatusChange(e.target.value as ApplicationStatus | "")}
        className="w-auto"
      >
        <option value="">{t("allStatuses")}</option>
        {APPLICATION_STATUSES.map((s) => (
          <option key={s} value={s}>
            {tStatus(s)}
          </option>
        ))}
      </Select>
      <Select
        value={sortBy}
        onChange={(e) => onSortByChange(e.target.value as ApplicationListSortBy)}
        className="w-auto"
      >
        {SORT_OPTIONS.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </Select>
      <Select
        value={sortDirection}
        onChange={(e) => onSortDirectionChange(e.target.value as SortDirection)}
        className="w-auto"
      >
        <option value="Descending">{t("descending")}</option>
        <option value="Ascending">{t("ascending")}</option>
      </Select>
    </div>
  );
}
