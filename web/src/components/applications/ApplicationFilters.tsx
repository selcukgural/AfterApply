"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationStatus, SortDirection } from "@/types/api";
import type { ListView } from "@/lib/applications/listView";
import { COMPANY_SORT_OPTIONS, FLAT_SORT_OPTIONS } from "@/lib/applications/listView";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import { looksLikeJobLink } from "@/lib/applications/pastedLink";
import { QUICK_FIND_OPEN_EVENT } from "@/lib/quickFind/quickFind";
import { useQuickFindShortcutLabel } from "@/hooks/useShortcutLabel";
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
  /** A posting link was pasted into search: searched at once, without the typing debounce, so
   *  the page can jump to the application when exactly one matches. */
  onLinkPasted?: (link: string) => void;
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
  onLinkPasted,
  onStatusChange,
  onSortByChange,
  onSortDirectionChange,
}: ApplicationFiltersProps) {
  const t = useTranslations("applications.filters");
  const tStatus = useTranslations("status");
  const shortcut = useQuickFindShortcutLabel();
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

  // A pasted link is handed to the page at once (onLinkPasted), which decides between opening the
  // application and filtering; the debounce must not send it a second time behind that decision.
  const pastedLinkRef = useRef<string | null>(null);

  useEffect(() => {
    const timeout = setTimeout(() => {
      if (searchInput !== search && searchInput !== pastedLinkRef.current) {
        onSearchChange(searchInput);
      }
    }, 300);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [searchInput]);

  return (
    <div className="flex flex-wrap items-end gap-3">
      {leading}
      <div className="relative min-w-48 flex-1">
        <Input
          className="md:pr-12"
          placeholder={t("searchPlaceholder")}
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          onPaste={(e) => {
            const pasted = e.clipboardData.getData("text");
            if (!onLinkPasted || !looksLikeJobLink(pasted)) return;
            e.preventDefault();
            const link = pasted.trim();
            pastedLinkRef.current = link;
            setSearchInput(link);
            onLinkPasted(link);
          }}
        />
        {/* Where people already look for an application: the one place that tells them the quick
            find exists. Keyboard-only users reach it with the shortcut it names, so it stays out
            of the tab order; a phone has no shortcut, and gets the search card instead. */}
        <button
          type="button"
          tabIndex={-1}
          onClick={() => window.dispatchEvent(new Event(QUICK_FIND_OPEN_EVENT))}
          title={t("quickFindHint", { shortcut })}
          aria-label={t("quickFindHint", { shortcut })}
          className="absolute right-2 top-1/2 hidden -translate-y-1/2 rounded border border-gray-200 bg-gray-50 px-1.5 py-0.5 font-mono text-[11px] text-gray-500 hover:text-gray-900 md:block dark:border-gray-700 dark:bg-gray-800 dark:text-gray-400 dark:hover:text-gray-100"
        >
          {shortcut}
        </button>
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
