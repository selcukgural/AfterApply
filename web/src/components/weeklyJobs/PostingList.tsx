"use client";

import type { MouseEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { JobSourcePostingSummaryResponse } from "@/types/api";
import { sourceLabel } from "@/lib/weeklyJobs/score";
import { ScoreBadge } from "./ScoreBadge";

interface PostingListProps {
  items: JobSourcePostingSummaryResponse[];
  selectedId: string | null;
  onSelect: (id: string) => void;
}

/** Below this width the page has no detail column, and a row is a link to the posting's own page. */
const DESKTOP_QUERY = "(min-width: 768px)";

/**
 * The left column: one row per posting, best score first (the API's order). Each row is a real
 * link to /weekly-jobs/{id}, so it works with no JavaScript and on a phone; on a desktop the
 * click is intercepted and only selects, because the detail is already on screen.
 */
export function PostingList({ items, selectedId, onSelect }: PostingListProps) {
  const t = useTranslations("weeklyJobs");
  const locale = useLocale();

  const handleClick = (id: string) => (event: MouseEvent<HTMLAnchorElement>) => {
    if (typeof window !== "undefined" && window.matchMedia(DESKTOP_QUERY).matches) {
      event.preventDefault();
      onSelect(id);
    }
  };

  const meta = (item: JobSourcePostingSummaryResponse) =>
    [sourceLabel(item.source), item.seniority, item.postedAt ? new Date(item.postedAt).toLocaleDateString(locale) : null]
      .filter((part): part is string => Boolean(part))
      .join(" · ");

  return (
    <ul className="flex flex-col gap-2" aria-label={t("list.ariaLabel")}>
      {items.map((item) => {
        const selected = item.id === selectedId;
        return (
          <li key={item.id}>
            <Link
              href={`/weekly-jobs/${item.id}`}
              onClick={handleClick(item.id)}
              aria-current={selected ? "true" : undefined}
              className={`grid grid-cols-[2.75rem_minmax(0,1fr)_1rem] items-center gap-3 rounded-lg border bg-white px-3.5 py-3 transition-colors hover:border-gray-300 md:grid-cols-[2.75rem_minmax(0,1fr)] dark:bg-gray-900 dark:hover:border-gray-700 ${
                // The ring says "this is the one open on the right" — meaningless below md, where
                // there is no right column and every row is a link to its own page.
                selected
                  ? "border-gray-200 md:border-accent md:ring-1 md:ring-inset md:ring-accent dark:border-gray-800"
                  : "border-gray-200 dark:border-gray-800"
              }`}
            >
              <ScoreBadge score={item.score} unscoredLabel={t("list.unscored")} scoreLabel={t("list.scoreLabel")} />
              <span className="flex min-w-0 flex-col gap-0.5">
                <span className="truncate text-sm font-medium text-gray-900 dark:text-gray-100">{item.title}</span>
                <span className="truncate text-sm text-gray-700 dark:text-gray-300">
                  {item.companyName}
                  {item.location ? ` · ${item.location}` : ""}
                </span>
                <span className="text-xs text-gray-500 dark:text-gray-400">
                  {item.score === null ? `${sourceLabel(item.source)} · ${t("list.unscored")}` : meta(item)}
                </span>
              </span>
              <svg
                viewBox="0 0 24 24"
                className="h-4 w-4 text-gray-400 md:hidden"
                fill="none"
                stroke="currentColor"
                strokeWidth={2}
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M9 6l6 6-6 6" />
              </svg>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
