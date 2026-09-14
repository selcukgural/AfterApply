"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";

/**
 * What an account without the paid plan sees. Deliberately without a price or a working button:
 * the payment integration is the next piece of work, and a "Buy" that goes nowhere is worse
 * than "Coming soon". The description is the feature as it exists, not a promise list.
 */
export function ProGate() {
  const t = useTranslations("weeklyJobs.gate");
  return (
    <div className="grid gap-6 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900 sm:grid-cols-[minmax(0,1fr)_220px]">
      <div className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("body")}</p>
        <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-gray-700 dark:text-gray-300">
          <li>{t("point1")}</li>
          <li>{t("point2")}</li>
          <li>{t("point3")}</li>
        </ul>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {t("privacy")}{" "}
          <Link href="/privacy#job-matching" className="text-accent-ink underline-offset-2 hover:underline">
            {t("privacyLink")}
          </Link>
        </p>
      </div>
      <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-950">
        <span className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("plan")}</span>
        <span className={buttonClassName("primary", "text-center opacity-40")} aria-disabled="true">
          {t("comingSoon")}
        </span>
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("comingSoonNote")}</p>
      </div>
    </div>
  );
}
