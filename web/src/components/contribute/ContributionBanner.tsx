"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ContributeTab } from "@/lib/contribute/contributeState";

/**
 * The thank-you after a save (design canvas 3B, 2026-09-16): a status line above the other
 * side's form, which is already open for the same company. `saved` is what was just written;
 * the invitation is for the other kind. "Skip" goes to the author's own list, where the saved
 * row is. `role="status"` so a screen reader hears it without focus moving.
 */
export function ContributionBanner({ saved, company }: { saved: ContributeTab; company: string }) {
  const t = useTranslations("contribute.banner");
  const skipHref = saved === "salary" ? "/my-salaries" : "/my-reviews";

  return (
    <div
      role="status"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-good/40 bg-good-wash px-4 py-3 text-sm"
    >
      <div className="flex items-center gap-3">
        <span className="inline-flex h-7 w-7 shrink-0 items-center justify-center text-good-ink">
          <svg viewBox="0 0 24 24" className="h-6 w-6" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" d="M4.5 12.75l6 6 9-13.5" />
          </svg>
        </span>
        <p className="text-gray-900 dark:text-gray-100">
          <strong>{t(`${saved}.thanks`)}</strong> {t(`${saved}.invite`, { company })}
        </p>
      </div>
      <Link href={skipHref} className="whitespace-nowrap text-gray-600 underline-offset-2 hover:underline dark:text-gray-400">
        {t(`${saved}.skip`)}
      </Link>
    </div>
  );
}
