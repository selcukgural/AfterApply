"use client";

import { useTranslations } from "next-intl";
import type { ListView } from "@/lib/applications/listView";

/** Two ways of reading the same applications. A segmented control rather than a dropdown because
 *  there are only two, and which one you are in should be readable without opening anything. */
export function ApplicationViewToggle({
  view,
  onViewChange,
}: {
  view: ListView;
  onViewChange: (view: ListView) => void;
}) {
  const t = useTranslations("applications.view");
  const views: { value: ListView; label: string }[] = [
    { value: "flat", label: t("flat") },
    { value: "company", label: t("company") },
  ];

  return (
    <div
      role="group"
      aria-label={t("label")}
      className="inline-flex overflow-hidden rounded-md border border-gray-300 dark:border-gray-700"
    >
      {views.map((option) => {
        const isActive = option.value === view;
        return (
          <button
            key={option.value}
            type="button"
            aria-pressed={isActive}
            onClick={() => onViewChange(option.value)}
            className={`px-3 py-2 text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-blue-500 ${
              isActive
                ? "bg-accent text-white"
                : "bg-white text-gray-600 hover:bg-gray-50 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
            }`}
          >
            {option.label}
          </button>
        );
      })}
    </div>
  );
}
