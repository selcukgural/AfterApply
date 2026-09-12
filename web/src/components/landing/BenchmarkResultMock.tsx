import { useTranslations } from "next-intl";
import { BenchmarkMedianCard, BenchmarkYourRateCard } from "@/components/benchmark/BenchmarkResultCards";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";

/**
 * What a /benchmark answer looks like, with fixed figures, rendered by the result screen's own two
 * cards. Sector scope on purpose: the demo shows the page at its most useful (a median from the
 * reader's own field), and the copy beside it says that state is not guaranteed.
 */
const DEMO = {
  yourRate: 23.3,
  replyCount: 7,
  applicationCount: 30,
  sector: "SoftwareAndIt",
  sampleSize: 212,
  minimumSampleSize: 30,
  comparedAgainstCount: 212,
  shareBelowYou: 38,
  medianRate: 31,
} as const;

export function BenchmarkResultMock({ className = "" }: { className?: string }) {
  const tMock = useTranslations("landing.tools.panels.benchmark");

  return (
    <div className={`relative ${className}`}>
      <SampleDataBadge className="-bottom-2.5 left-3" />
      <div role="img" aria-label={tMock("mockAriaLabel")}>
        <div aria-hidden="true" className="flex flex-col gap-4">
          <BenchmarkYourRateCard
            yourRate={DEMO.yourRate}
            replyCount={DEMO.replyCount}
            applicationCount={DEMO.applicationCount}
          />
          <BenchmarkMedianCard
            scope="Sector"
            sector={DEMO.sector}
            sampleSize={DEMO.sampleSize}
            minimumSampleSize={DEMO.minimumSampleSize}
            comparedAgainstCount={DEMO.comparedAgainstCount}
            shareBelowYou={DEMO.shareBelowYou}
            yourRate={DEMO.yourRate}
            medianRate={DEMO.medianRate}
            showSavedNote={false}
          />
        </div>
      </div>
    </div>
  );
}
