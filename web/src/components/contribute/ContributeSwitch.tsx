"use client";

import { useTranslations } from "next-intl";
import type { ContributeTab } from "@/lib/contribute/contributeState";

/** The sides of the contribute page as a segmented control (the applications list's view
 *  toggle): few enough that which one is open should be readable without opening anything. The
 *  caller passes the sides that are switched on, in display order. */
export function ContributeSwitch({ tabs, value, onChange }: { tabs: readonly ContributeTab[]; value: ContributeTab; onChange: (tab: ContributeTab) => void }) {
  const t = useTranslations("contribute.tabs");

  return (
    <div role="group" aria-label={t("label")} className="inline-flex overflow-hidden rounded-md border border-gray-300 dark:border-gray-700">
      {tabs.map((tab) => {
        const isActive = tab === value;
        return (
          <button
            key={tab}
            type="button"
            aria-pressed={isActive}
            onClick={() => onChange(tab)}
            className={`px-3 py-2 text-sm font-medium transition-colors focus:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-blue-500 ${
              isActive
                ? "bg-accent text-white"
                : "bg-white text-gray-600 hover:bg-gray-50 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
            }`}
          >
            {t(tab)}
          </button>
        );
      })}
    </div>
  );
}
