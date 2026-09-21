"use client";

import { useState, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { navLinkClassName } from "@/components/layout/navLink";
import { useCompanyIntelligence } from "@/components/companyIntelligence/CompanyIntelligencePanel";

type CompanyTab = "reviews" | "salaries" | "experiences" | "intelligence";
const TAB_KEYS: readonly CompanyTab[] = ["reviews", "salaries", "experiences", "intelligence"];

/**
 * "Reviews | Salaries | Candidate experience | Response" on a company page. The server still
 * renders the reviews half with its data (the HTML carries them, for crawlers); this only decides
 * which half is on screen. The other three are client fetches and never in the cached page.
 * `?tab=` preselects one — that is where a sign-in card and the contribute page's links land —
 * and each tab is gated by its own flag: with all of them off the row is not rendered at all, so
 * the page looks as it did before any of the features.
 *
 * The response tab (2026-09-22) carries its application count only once the company is above
 * the threshold: below it the tab is a word, because the count is the very thing withheld. The
 * count comes from the same query the panel reads, so the strip costs no request of its own.
 */
export function CompanyPageTabs({
  companyId,
  reviewCount,
  salaryCount,
  experienceCount,
  reviews,
  salaries,
  experiences,
  intelligence,
}: {
  companyId: string;
  reviewCount: number;
  salaryCount: number;
  experienceCount: number;
  reviews: ReactNode;
  salaries: ReactNode;
  experiences: ReactNode;
  intelligence: ReactNode;
}) {
  const t = useTranslations("companies.page.tabs");
  const { config } = useClientConfig();
  const requested = useSearchParams().get("tab");
  const salariesOn = config.companySalaries?.enabled === true;
  const experiencesOn = config.candidateExperiences?.enabled === true;
  const intelligenceOn = config.companyIntelligence?.enabled === true;
  const [tab, setTab] = useState<CompanyTab>(
    requested !== null && requested !== "reviews" && TAB_KEYS.includes(requested as CompanyTab) ? (requested as CompanyTab) : "reviews",
  );
  const intelligenceQuery = useCompanyIntelligence(companyId, intelligenceOn);

  if (!salariesOn && !experiencesOn && !intelligenceOn) {
    return <>{reviews}</>;
  }

  const tabs: { key: CompanyTab; count: number | null }[] = [
    { key: "reviews", count: reviewCount },
    ...(salariesOn ? [{ key: "salaries" as const, count: salaryCount }] : []),
    ...(experiencesOn ? [{ key: "experiences" as const, count: experienceCount }] : []),
    ...(intelligenceOn
      ? [{ key: "intelligence" as const, count: intelligenceQuery.data?.metrics?.totalApplications ?? null }]
      : []),
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
              {item.count !== null && <span className="text-xs text-gray-500 dark:text-gray-400">{item.count}</span>}
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
      {intelligenceOn ? (
        <div role="tabpanel" id="company-panel-intelligence" aria-labelledby="company-tab-intelligence" hidden={active !== "intelligence"}>
          {active === "intelligence" ? intelligence : null}
        </div>
      ) : null}
    </div>
  );
}
