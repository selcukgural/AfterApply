"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { navLinkClassName } from "@/components/layout/navLink";

const TABS = [
  { href: "/admin/metrics", key: "metrics" },
  { href: "/admin/reviews", key: "reviews" },
  { href: "/admin/reviews/reports", key: "reports" },
] as const;

/**
 * The admin area's navigation. There is no admin layout — each page mounts this at the top — so
 * a second page needed a way to reach the first without the user typing a URL, which is the
 * whole reason the admin link exists in the menu at all (DECISIONS.md, 2026-09-10).
 */
export function AdminTabs() {
  const t = useTranslations("adminTabs");
  const pathname = usePathname();
  const { data: counts } = useQuery({
    queryKey: ["admin", "moderationCounts"],
    queryFn: adminApi.getModerationCounts,
    refetchInterval: 60_000,
    // A 403 or a 404 (feature dark) is a settled answer, not a hiccup.
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const badge = (key: (typeof TABS)[number]["key"]) => {
    const value = key === "reviews" ? counts?.pendingReviews : key === "reports" ? counts?.openReports : 0;
    return value ? (
      <span className="inline-flex min-w-[1.25rem] items-center justify-center rounded-full bg-blue-600 px-1.5 py-0.5 text-xs font-semibold leading-none text-white">
        {value}
      </span>
    ) : null;
  };

  return (
    <nav aria-label={t("label")} className="flex flex-wrap gap-1 border-b border-gray-200 text-sm dark:border-gray-800">
      {TABS.map((tab) => {
        const active = pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            aria-current={active ? "page" : undefined}
            className={navLinkClassName("underline", active, "-mb-px flex items-center gap-1.5 px-3 py-2")}
          >
            {t(tab.key)}
            {badge(tab.key)}
          </Link>
        );
      })}
    </nav>
  );
}
