"use client";

import { useTranslations } from "next-intl";
import type { ReviewModerationStatus } from "@/types/api";

const CLASSES: Record<ReviewModerationStatus, string> = {
  Pending: "bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300",
  Approved: "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300",
  Rejected: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
};

export function ReviewStatusBadge({ status }: { status: ReviewModerationStatus }) {
  const t = useTranslations("reviewModerationStatus");
  return (
    <span className={`inline-flex rounded-full px-2.5 py-0.5 text-xs font-medium ${CLASSES[status]}`}>{t(status)}</span>
  );
}
