"use client";

import { useTranslations } from "next-intl";
import type { StatusChangeOrigin } from "@/types/api";

// Colour separates "a person decided this" from "the system decided this" — the distinction the
// chip exists to make. Both email origins share the family; the auto-applied one is the stronger
// tone because it is the change nobody approved.
const ORIGIN_COLORS: Record<StatusChangeOrigin, string> = {
  Manual: "border-gray-300 text-gray-600 dark:border-gray-700 dark:text-gray-400",
  EmailSuggestionConfirmed: "border-blue-300 text-blue-700 dark:border-blue-800 dark:text-blue-300",
  EmailAutoApplied: "border-purple-300 text-purple-700 dark:border-purple-800 dark:text-purple-300",
  // Both "the user took it back" origins share the neutral person tone: what they record is a
  // person correcting something, not the mechanism that made the mistake.
  EmailAutoApplyReverted: "border-gray-300 text-gray-600 dark:border-gray-700 dark:text-gray-400",
  BulkEdit: "border-gray-300 text-gray-600 dark:border-gray-700 dark:text-gray-400",
  BulkEditReverted: "border-gray-300 text-gray-600 dark:border-gray-700 dark:text-gray-400",
  Import: "border-teal-300 text-teal-700 dark:border-teal-800 dark:text-teal-300",
  Extension: "border-teal-300 text-teal-700 dark:border-teal-800 dark:text-teal-300",
  System: "border-gray-300 text-gray-600 dark:border-gray-700 dark:text-gray-400",
};

export function OriginChip({ origin }: { origin: StatusChangeOrigin }) {
  const t = useTranslations("statusChangeOrigin");
  return (
    <span
      className={`inline-flex shrink-0 items-center gap-1.5 rounded border px-2 py-0.5 text-[11px] font-medium ${ORIGIN_COLORS[origin]}`}
    >
      <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-current" />
      {t(origin)}
    </span>
  );
}
