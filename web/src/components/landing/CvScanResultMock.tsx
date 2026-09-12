import { useTranslations } from "next-intl";
import { CvScanCategoryBars, CvScanScoreCard } from "@/components/cvScan/CvScanResultCards";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";
import type { CvScanCategoryScore } from "@/types/api";

/**
 * What a /cv-tarama result looks like, with fixed figures: the real score card and category bars
 * (the same components the result screen renders), plus one finding card in the result screen's
 * own words. The numbers add up on purpose — 32+22+15+9 = 78 — because the scan's pitch is that a
 * reader can check the arithmetic, and a demo that did not would teach the wrong thing.
 *
 * One picture for assistive technology (`role="img"`), like the extension mock: the headings and
 * bars inside are hidden so they do not join the landing page's own outline.
 */
const DEMO_CATEGORIES: CvScanCategoryScore[] = [
  { category: "MachineReadability", weight: 40, score: 32 },
  { category: "SectionsAndDates", weight: 25, score: 22 },
  { category: "Contact", weight: 15, score: 15 },
  { category: "FormatAndLength", weight: 20, score: 9 },
];
const DEMO_SCORE = 78;
const DEMO_FINDING_COST = 11;

export function CvScanResultMock({ className = "" }: { className?: string }) {
  const t = useTranslations("cvScan");
  const tMock = useTranslations("landing.tools.panels.cv");

  return (
    <div className={`relative ${className}`}>
      <SampleDataBadge className="-bottom-2.5 left-3" />
      <div role="img" aria-label={tMock("mockAriaLabel")}>
        <div aria-hidden="true" className="flex flex-col gap-4">
          <CvScanScoreCard score={DEMO_SCORE} />
          <CvScanCategoryBars categories={DEMO_CATEGORIES} />
          <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">
                {t("findings.MultiColumnOrTableLayout.title")}
              </span>
              <span className="shrink-0 rounded-full bg-gray-100 px-2 py-0.5 text-xs tabular-nums text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                {t("result.cost", { points: DEMO_FINDING_COST })}
              </span>
            </div>
            <p className="text-sm text-gray-600 dark:text-gray-400">
              <span className="font-medium text-gray-800 dark:text-gray-200">{t("result.fix")}: </span>
              {t("findings.MultiColumnOrTableLayout.fix")}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
