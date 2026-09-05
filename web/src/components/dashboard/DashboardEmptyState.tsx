"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";

/**
 * What a brand-new account sees instead of twelve tiles reading "0". Every figure on this
 * dashboard is derived from applications, so with none there is nothing to derive — say that and
 * point at the two ways to get some in.
 */
export function DashboardEmptyState() {
  const t = useTranslations("dashboard.empty");

  return (
    <div className="flex flex-col items-start gap-4 rounded-xl border border-dashed border-gray-300 bg-white p-8 dark:border-gray-700 dark:bg-gray-900">
      <div className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="max-w-[52ch] text-sm text-gray-600 dark:text-gray-400">{t("body")}</p>
      </div>
      <div className="flex flex-wrap gap-3">
        <Link href="/applications/new" className={buttonClassName("primary")}>
          {t("cta")}
        </Link>
        <Link href="/import" className={buttonClassName("secondary")}>
          {t("importCta")}
        </Link>
      </div>
    </div>
  );
}
