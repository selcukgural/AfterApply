"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationStatus, SortDirection } from "@/types/api";
import type { ListView } from "@/lib/applications/listView";
import { COMPANY_SORT_OPTIONS, FLAT_SORT_OPTIONS } from "@/lib/applications/listView";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";

interface ApplicationFiltersProps {
  /** Which view's sort options to offer. Search and status mean the same thing in both — they
   *  narrow applications — so only the sort list changes. */
  view: ListView;
  /** Rendered as the first control in the row — the view switch. It belongs on this line rather
   *  than above it (it decides what the controls beside it mean), and it has to be a *slot* rather
   *  than a nested element: a wrapper of its own would become a flex item and drag the whole filter
   *  row into it, wrapping every control onto its own line. */
  leading?: React.ReactNode;
  search: string;
  status: ApplicationStatus | "";
  sortBy: string;
  sortDirection: SortDirection;
  onSearchChange: (search: string) => void;
  onStatusChange: (status: ApplicationStatus | "") => void;
  /** Raw, because the two views have different sort unions; the page validates it back into the
   *  one its own view accepts. */
  onSortByChange: (sortBy: string) => void;
  onSortDirectionChange: (direction: SortDirection) => void;
}

export function ApplicationFilters({
  view,
  leading,
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
  const SORT_LABELS: Record<string, string> = {
    AppliedAt: t("sortAppliedAt"),
    UpdatedAt: t("sortUpdatedAt"),
    CompanyName: t("sortCompany"),
    JobTitle: t("sortTitle"),
    Status: t("sortStatus"),
    LastActivity: t("sortLastActivity"),
    ApplicationCount: t("sortApplicationCount"),
  };
  const sortOptions = view === "company" ? COMPANY_SORT_OPTIONS : FLAT_SORT_OPTIONS;

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
      {leading}
      <div className="min-w-48 flex-1">
        <Input
          placeholder={t("searchPlaceholder")}
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
        />
      </div>
      {/* Each select is sized by its wrapper, not by a class on itself: Select hardcodes `w-full`,
          and a `w-auto` passed alongside it loses or wins purely on stylesheet order — the same
          collision that made a button read as disabled in the bulk toolbar. The wrapper decides the
          width and the select fills it, which no cascade can undo. */}
      <div className="w-48">
        <Select value={status} onChange={(e) => onStatusChange(e.target.value as ApplicationStatus | "")}>
          <option value="">{t("allStatuses")}</option>
          {APPLICATION_STATUSES.map((s) => (
            <option key={s} value={s}>
              {tStatus(s)}
            </option>
          ))}
        </Select>
      </div>
      <div className="w-44">
        <Select value={sortBy} onChange={(e) => onSortByChange(e.target.value)}>
          {sortOptions.map((option) => (
            <option key={option} value={option}>
              {SORT_LABELS[option]}
            </option>
          ))}
        </Select>
      </div>
      <div className="w-32">
        <Select value={sortDirection} onChange={(e) => onSortDirectionChange(e.target.value as SortDirection)}>
          <option value="Descending">{t("descending")}</option>
          <option value="Ascending">{t("ascending")}</option>
        </Select>
      </div>
    </div>
  );
}
