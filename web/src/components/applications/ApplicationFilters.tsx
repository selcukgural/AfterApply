"use client";

import { useEffect, useState } from "react";
import type { ApplicationListSortBy, ApplicationStatus, SortDirection } from "@/types/api";
import { APPLICATION_STATUSES, APPLICATION_STATUS_LABELS } from "@/lib/constants/applicationStatus";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";

const SORT_OPTIONS: { value: ApplicationListSortBy; label: string }[] = [
  { value: "AppliedAt", label: "Başvuru Tarihi" },
  { value: "UpdatedAt", label: "Son Güncelleme" },
  { value: "CompanyName", label: "Şirket" },
  { value: "JobTitle", label: "Pozisyon" },
  { value: "Status", label: "Durum" },
];

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
          placeholder="Şirket veya pozisyon ara..."
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
      </div>
      <Select
        value={status}
        onChange={(e) => onStatusChange(e.target.value as ApplicationStatus | "")}
        className="w-auto"
      >
        <option value="">Tüm Durumlar</option>
        {APPLICATION_STATUSES.map((s) => (
          <option key={s} value={s}>
            {APPLICATION_STATUS_LABELS[s]}
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
        <option value="Descending">Azalan</option>
        <option value="Ascending">Artan</option>
      </Select>
    </div>
  );
}
