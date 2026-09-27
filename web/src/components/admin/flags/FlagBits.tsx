"use client";

import { useTranslations } from "next-intl";
import type { FeatureFlagCoupling } from "@/types/api";

/** Açık / Kapalı: green for on, grey for off — told apart by lightness and by the word, not hue alone. */
export function FlagStatePill({ on }: { on: boolean }) {
  const t = useTranslations("adminFlags.state");
  return (
    <span
      className={`inline-flex items-center rounded-full px-3 py-1 text-[13px] font-semibold ${
        on ? "bg-green-100 text-green-800 dark:bg-green-950 dark:text-green-300" : "bg-gray-200 text-gray-700 dark:bg-gray-800 dark:text-gray-300"
      }`}
    >
      {t(on ? "on" : "off")}
    </span>
  );
}

/** What moves with a flag: the privacy text (blue) and money (orange). */
export function FlagCouplings({ couplings }: { couplings: readonly FeatureFlagCoupling[] }) {
  const t = useTranslations("adminFlags.couplings");
  return (
    <>
      {couplings.map((coupling) => (
        <span
          key={coupling}
          className={`rounded-full px-2 py-0.5 text-xs font-medium ${
            coupling === "Money"
              ? "bg-orange-100 text-orange-900 dark:bg-orange-950 dark:text-orange-300"
              : "bg-accent-wash text-accent-ink"
          }`}
        >
          {t(coupling)}
        </span>
      ))}
    </>
  );
}

export function WarningIcon({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={className} fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z" />
      <path d="M12 9v4" />
      <path d="M12 17h.01" />
    </svg>
  );
}
