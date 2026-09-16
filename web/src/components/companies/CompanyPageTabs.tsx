"use client";

import { useState, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { navLinkClassName } from "@/components/layout/navLink";

type CompanyTab = "reviews" | "salaries";

/**
 * "Reviews | Salaries" on a company page. The server still renders the reviews half with its
 * data (the HTML carries them, for crawlers); this only decides which half is on screen. The
 * salaries half is a client fetch behind sign-in and never in the cached page. `?tab=salaries`
 * preselects it — that is where the sign-in card and the contribute page's links land — and
 * while the salary feature is off the row is not rendered at all, so the page looks as before.
 */
export function CompanyPageTabs({
  reviewCount,
  salaryCount,
  reviews,
  salaries,
}: {
  reviewCount: number;
  salaryCount: number;
  reviews: ReactNode;
  salaries: ReactNode;
}) {
  const t = useTranslations("companies.page.tabs");
  const { config } = useClientConfig();
  const requested = useSearchParams().get("tab");
  const [tab, setTab] = useState<CompanyTab>(requested === "salaries" ? "salaries" : "reviews");

  if (!config.companySalaries?.enabled) {
    return <>{reviews}</>;
  }

  const tabs: { key: CompanyTab; count: number }[] = [
    { key: "reviews", count: reviewCount },
    { key: "salaries", count: salaryCount },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div role="tablist" aria-label={t("label")} className="flex gap-1 border-b border-gray-200 text-sm dark:border-gray-800">
        {tabs.map((item) => {
          const active = tab === item.key;
          return (
            <button
              key={item.key}
              type="button"
              role="tab"
              id={`company-tab-${item.key}`}
              aria-selected={active}
              aria-controls={`company-panel-${item.key}`}
              onClick={() => setTab(item.key)}
              className={navLinkClassName("underline", active, "-mb-px flex items-center gap-1.5 px-3 py-2")}
            >
              {t(item.key)}
              <span className="text-xs text-gray-500 dark:text-gray-400">{item.count}</span>
            </button>
          );
        })}
      </div>
      <div role="tabpanel" id="company-panel-reviews" aria-labelledby="company-tab-reviews" hidden={tab !== "reviews"} className="flex flex-col gap-8">
        {reviews}
      </div>
      <div role="tabpanel" id="company-panel-salaries" aria-labelledby="company-tab-salaries" hidden={tab !== "salaries"}>
        {tab === "salaries" ? salaries : null}
      </div>
    </div>
  );
}
