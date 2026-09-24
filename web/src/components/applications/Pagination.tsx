"use client";

import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";

const PAGE_INFO_KEY = {
  applications: "pageInfo",
  companies: "pageInfoCompanies",
  reminders: "pageInfoReminders",
  orders: "pageInfoOrders",
  salaries: "pageInfoSalaries",
  experiences: "pageInfoExperiences",
  notifications: "pageInfoNotifications",
  alerts: "pageInfoAlerts",
  reviews: "pageInfoReviews",
  contributions: "pageInfoContributions",
  posts: "pageInfoPosts",
  comments: "pageInfoComments",
} as const;

interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  /** What a page is made of. The company view pages over companies, and telling someone they are on
   *  "page 2 of 4 (34 applications)" while the pages hold companies gives them a number they cannot
   *  check against what is on screen. */
  unit?:
    | "applications"
    | "companies"
    | "reminders"
    | "orders"
    | "salaries"
    | "experiences"
    | "notifications"
    | "alerts"
    | "reviews"
    | "contributions"
    | "posts"
    | "comments";
  onPageChange: (page: number) => void;
}

export function Pagination({
  page,
  pageSize,
  totalCount,
  unit = "applications",
  onPageChange,
}: PaginationProps) {
  const t = useTranslations("applications.pagination");
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (totalPages <= 1) {
    return null;
  }

  return (
    // Wraps on a phone (2026-09-24): the four English labels are ~300px on their own, and next to
    // the page count they pushed a 390px screen sideways; the buttons now take their own line.
    <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 text-sm text-gray-600 dark:text-gray-400">
      <span>
        {t(PAGE_INFO_KEY[unit], { page, totalPages, totalCount })}
      </span>
      <div className="flex flex-wrap gap-2">
        <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(1)}>
          {t("first")}
        </Button>
        <Button variant="secondary" disabled={page <= 1} onClick={() => onPageChange(page - 1)}>
          {t("previous")}
        </Button>
        <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)}>
          {t("next")}
        </Button>
        <Button variant="secondary" disabled={page >= totalPages} onClick={() => onPageChange(totalPages)}>
          {t("last")}
        </Button>
      </div>
    </div>
  );
}
