"use client";

import { useState, type ReactNode } from "react";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useClientConfig } from "@/hooks/useClientConfig";
import { navLinkClassName } from "@/components/layout/navLink";
import { useCompanyIntelligence } from "@/components/companyIntelligence/CompanyIntelligencePanel";
import type { CompanyPageLead, CompanyTabFeatures } from "@/lib/companies/companyPageContent";

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
 * Which tabs are on comes from the server's read of the config until the browser has its own
 * (`serverFeatures`), so the row is in the HTML as the visitor will see it. `lead` is the half that
 * opens without a `?tab=` (2026-10-14): a company known only for candidate experiences opens on
 * them, that tab comes first, and its empty reviews half stays out of the HTML until it is opened
 * — a crawler reads the experiences, not "no reviews yet".
 *
 * The response tab (2026-09-22) carries its application count only once the company is above
 * the threshold: below it the tab is a word, because the count is the very thing withheld. The
 * count comes from the same query the panel reads, so the strip costs no request of its own.
 */
export function CompanyPageTabs({
  companyId,
  lead,
  serverFeatures,
  reviewCount,
  salaryCount,
  experienceCount,
  reviews,
  salaries,
  experiences,
  intelligence,
}: {
  companyId: string;
  lead: CompanyPageLead;
  serverFeatures: CompanyTabFeatures | null;
  reviewCount: number;
  salaryCount: number;
  experienceCount: number;
  reviews: ReactNode;
  salaries: ReactNode;
  experiences: ReactNode;
  intelligence: ReactNode;
}) {
  const t = useTranslations("companies.page.tabs");
  const { config, isLoaded } = useClientConfig();
  const requested = useSearchParams().get("tab");
  const features: CompanyTabFeatures =
    !isLoaded && serverFeatures
      ? serverFeatures
      : {
          salaries: config.companySalaries?.enabled === true,
          experiences: config.candidateExperiences?.enabled === true,
          intelligence: config.companyIntelligence?.enabled === true,
        };
  const { salaries: salariesOn, experiences: experiencesOn, intelligence: intelligenceOn } = features;
  const [tab, setTab] = useState<CompanyTab>(
    requested !== null && TAB_KEYS.includes(requested as CompanyTab) ? (requested as CompanyTab) : lead,
  );
  const intelligenceQuery = useCompanyIntelligence(companyId, intelligenceOn);

  if (!salariesOn && !experiencesOn && !intelligenceOn) {
    return <>{reviews}</>;
  }

  const experiencesTab = experiencesOn ? [{ key: "experiences" as const, count: experienceCount }] : [];
  const leadsWithExperiences = lead === "experiences" && experiencesOn;
  const tabs: { key: CompanyTab; count: number | null }[] = [
    ...(leadsWithExperiences ? experiencesTab : []),
    { key: "reviews", count: reviewCount },
    ...(salariesOn ? [{ key: "salaries" as const, count: salaryCount }] : []),
    ...(leadsWithExperiences ? [] : experiencesTab),
    ...(intelligenceOn
      ? [{ key: "intelligence" as const, count: intelligenceQuery.data?.metrics?.totalApplications ?? null }]
      : []),
  ];
  const active = tabs.some((item) => item.key === tab) ? tab : tabs[0].key;

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
        {/* With reviews, the half is always in the HTML (crawlers read it from any tab); without,
            it is only an empty state, rendered once someone opens it. */}
        {reviewCount > 0 || active === "reviews" ? reviews : null}
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
