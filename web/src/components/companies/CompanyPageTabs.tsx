"use client";

import { useState, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { navLinkClassName } from "@/components/layout/navLink";

type CompanyTab = "reviews" | "salaries" | "experiences";

/**
 * "Reviews | Salaries | Candidate experience" on a company page. The server still renders the
 * reviews half with its data (the HTML carries them, for crawlers); this only decides which
 * half is on screen. The other two are client fetches and never in the cached page. `?tab=`
 * preselects one — that is where a sign-in card and the contribute page's links land — and each
 * tab is gated by its own flag: with both off the row is not rendered at all, so the page looks
 * as it did before either feature.
 */
export function CompanyPageTabs({
  reviewCount,
  salaryCount,
  experienceCount,
  reviews,
  salaries,
  experiences,
}: {
  reviewCount: number;
  salaryCount: number;
  experienceCount: number;
  reviews: ReactNode;
  salaries: ReactNode;
  experiences: ReactNode;
}) {
  const t = useTranslations("companies.page.tabs");
  const { config } = useClientConfig();
  const requested = useSearchParams().get("tab");
  const salariesOn = config.companySalaries?.enabled === true;
  const experiencesOn = config.candidateExperiences?.enabled === true;
  const [tab, setTab] = useState<CompanyTab>(requested === "salaries" || requested === "experiences" ? requested : "reviews");

  if (!salariesOn && !experiencesOn) {
    return <>{reviews}</>;
  }

  const tabs: { key: CompanyTab; count: number }[] = [
    { key: "reviews", count: reviewCount },
    ...(salariesOn ? [{ key: "salaries" as const, count: salaryCount }] : []),
    ...(experiencesOn ? [{ key: "experiences" as const, count: experienceCount }] : []),
  ];
  const active = tabs.some((item) => item.key === tab) ? tab : "reviews";

  return (
    <div className="flex flex-col gap-6">
      <div role="tablist" aria-label={t("label")} className="flex gap-1 border-b border-gray-200 text-sm dark:border-gray-800">
        {tabs.map((item) => {
          const selected = active === item.key;
          return (
            <button
              key={item.key}
              type="button"
              role="tab"
              id={`company-tab-${item.key}`}
              aria-selected={selected}
              aria-controls={`company-panel-${item.key}`}
              onClick={() => setTab(item.key)}
              className={navLinkClassName("underline", selected, "-mb-px flex items-center gap-1.5 px-3 py-2")}
            >
              {t(item.key)}
              <span className="text-xs text-gray-500 dark:text-gray-400">{item.count}</span>
            </button>
          );
        })}
      </div>
      <div role="tabpanel" id="company-panel-reviews" aria-labelledby="company-tab-reviews" hidden={active !== "reviews"} className="flex flex-col gap-8">
        {reviews}
      </div>
      {salariesOn ? (
        <div role="tabpanel" id="company-panel-salaries" aria-labelledby="company-tab-salaries" hidden={active !== "salaries"}>
          {active === "salaries" ? salaries : null}
        </div>
      ) : null}
      {experiencesOn ? (
        <div role="tabpanel" id="company-panel-experiences" aria-labelledby="company-tab-experiences" hidden={active !== "experiences"}>
          {active === "experiences" ? experiences : null}
        </div>
      ) : null}
    </div>
  );
}
