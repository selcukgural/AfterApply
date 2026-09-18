"use client";

import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { ADMIN_CONTRIBUTION_TABS } from "@/lib/admin/adminTabs";

/**
 * The second row under the admin's "Reviews" tab (2026-09-18): the moderation queue, the salary
 * entries and the candidate experiences, one table each. Pills rather than a second underline
 * row so the two levels do not read as one.
 */
export function AdminContributionTabs() {
  const t = useTranslations("adminContributionTabs");
  const pathname = usePathname();

  return (
    <nav aria-label={t("label")} className="flex flex-wrap gap-2 text-sm">
      {ADMIN_CONTRIBUTION_TABS.map((tab) => {
        const active = pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? "page" : undefined}
            className={
              active
                ? "rounded-full border border-accent bg-accent-wash px-3 py-1.5 font-medium text-accent-ink"
                : "rounded-full border border-gray-300 px-3 py-1.5 text-gray-700 hover:border-gray-400 hover:text-gray-900 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500 dark:hover:text-gray-100"
            }
          >
            {t(tab.key)}
          </Link>
        );
      })}
    </nav>
  );
}
