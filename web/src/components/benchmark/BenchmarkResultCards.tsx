"use client";

import { useLocale, useTranslations } from "next-intl";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import type { BenchmarkResultResponse } from "@/types/api";

/**
 * The two cards a benchmark answer is made of, as presentation only: your rate with the two counts
 * behind it, and the median it is compared against.
 *
 * Split out of BenchmarkForm (2026-09-12) for the landing page, which shows them with fixed demo
 * figures instead of a screenshot. The three-state honesty rule of the page — a sector median must
 * never read as an overall one, and a withheld comparison is explained rather than shown — is
 * decided by the caller choosing which card to render with which `scope`; nothing here invents a
 * number.
 */

type RateCardProps = Pick<BenchmarkResultResponse, "yourRate" | "replyCount" | "applicationCount">;

export function BenchmarkYourRateCard({ yourRate, replyCount, applicationCount }: RateCardProps) {
  const t = useTranslations("benchmark");
  const locale = useLocale();

  return (
    <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <p className="text-sm text-gray-500 dark:text-gray-400">{t("result.yourRateLabel")}</p>
      <p className="mt-1 text-5xl font-semibold tracking-tight text-gray-900 tabular-nums dark:text-gray-100">
        {formatRate(yourRate, locale)}
      </p>
      {/* The two numbers the percentage came from. A rate with nothing behind it invites the
          reader to wonder whether they typed what they meant to. */}
      <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
        {t("result.yourRateDetail", {
          replies: formatCount(replyCount, locale),
          applications: formatCount(applicationCount, locale),
        })}
      </p>
    </div>
  );
}

type MedianCardProps = Pick<
  BenchmarkResultResponse,
  "scope" | "sector" | "sampleSize" | "minimumSampleSize" | "comparedAgainstCount" | "shareBelowYou" | "yourRate"
> & {
  /** Non-null by contract: the caller renders the withheld explanation instead when there is none. */
  medianRate: number;
  /** The "saved" line under the verdict — the real page says the answer was kept; the demo has
   *  nothing to say there. */
  showSavedNote?: boolean;
};

export function BenchmarkMedianCard({
  scope,
  sector,
  sampleSize,
  minimumSampleSize,
  comparedAgainstCount,
  shareBelowYou,
  yourRate,
  medianRate,
  showSavedNote = true,
}: MedianCardProps) {
  const t = useTranslations("benchmark");
  const locale = useLocale();

  const position = yourRate > medianRate ? "above" : yourRate < medianRate ? "below" : "at";

  return (
    <div className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
        {scope === "Sector"
          ? t("result.comparedTitle", {
              sector: t(`sectors.${sector}`),
              sampleSize: formatCount(sampleSize, locale),
            })
          : t("result.overallTitle", {
              sector: t(`sectors.${sector}`),
              sampleSize: formatCount(sampleSize, locale),
              minimum: formatCount(minimumSampleSize, locale),
            })}
      </p>

      <div className="flex flex-wrap items-baseline gap-x-3 gap-y-1">
        <span className="text-sm text-gray-500 dark:text-gray-400">
          {scope === "Sector" ? t("result.median") : t("result.overallMedianLabel")}
        </span>
        <span className="text-2xl font-semibold text-gray-900 tabular-nums dark:text-gray-100">
          {formatRate(medianRate, locale)}
        </span>
      </div>

      {scope === "Overall" && comparedAgainstCount !== null ? (
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {t("result.overallNote", { count: formatCount(comparedAgainstCount, locale) })}
        </p>
      ) : null}

      {shareBelowYou !== null ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("result.shareBelow", { share: formatCount(Math.round(shareBelowYou), locale) })}
        </p>
      ) : null}

      <p className="text-sm font-medium text-gray-900 dark:text-gray-100">
        {position === "above"
          ? t("result.aboveMedian")
          : position === "below"
            ? t("result.belowMedian")
            : t("result.atMedian")}
      </p>
      {showSavedNote ? <p className="text-xs text-gray-500 dark:text-gray-400">{t("result.saved")}</p> : null}
    </div>
  );
}
