"use client";

import { useTranslations } from "next-intl";
import type { ApplicationStatus } from "@/types/api";

const STATUS_COLORS: Record<ApplicationStatus, string> = {
  Applied: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
  Screening: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  Interview: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  TechnicalInterview: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  FinalInterview: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  Offer: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  Accepted: "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300",
  Rejected: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  Withdrawn: "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
  Ghosted: "bg-purple-100 text-purple-700 dark:bg-purple-900/40 dark:text-purple-300",
};

/**
 * The same palette as the badges above, reduced to a fill — for the company view's status
 * distribution, where a status is a segment of a bar rather than a word. Kept next to STATUS_COLORS
 * on purpose: a status whose badge is blue and whose bar is green would teach two different codes
 * for one thing.
 */
export const STATUS_BAR_COLORS: Record<ApplicationStatus, string> = {
  Applied: "bg-gray-300 dark:bg-gray-600",
  Screening: "bg-blue-300 dark:bg-blue-700",
  Interview: "bg-blue-400 dark:bg-blue-600",
  TechnicalInterview: "bg-blue-400 dark:bg-blue-600",
  FinalInterview: "bg-blue-500 dark:bg-blue-500",
  Offer: "bg-amber-400 dark:bg-amber-500",
  Accepted: "bg-green-500 dark:bg-green-500",
  Rejected: "bg-red-400 dark:bg-red-500",
  Withdrawn: "bg-gray-200 dark:bg-gray-700",
  Ghosted: "bg-purple-400 dark:bg-purple-500",
};

export function StatusBadge({ status }: { status: ApplicationStatus }) {
  const t = useTranslations("status");
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[status]}`}>
      {t(status)}
    </span>
  );
}
