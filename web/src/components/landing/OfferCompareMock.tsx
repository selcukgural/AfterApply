"use client";

import { useLocale, useTranslations } from "next-intl";
import { compareOffers } from "@/lib/offerCompare/compare";
import { exampleDrafts, formatLira, formatNumber, toOffer } from "@/lib/offerCompare/input";
import { OfferVerdict } from "@/components/offerCompare/OfferVerdict";
import { SampleDataBadge } from "@/components/landing/SampleDataBadge";

const BAR_HEIGHT = 88;

/**
 * What the offer comparison answers, with the page's own two example offers run through the real
 * calculation and drawn by the page's own verdict card — so the demo cannot show a number the tool
 * would not. Under it, the second offer's months in thousands: the bracket slide and the two bonus
 * months are the tool's point, and they fit in a strip this small only without the currency.
 */
export function OfferCompareMock({ className = "" }: { className?: string }) {
  const t = useTranslations("landing.tools.panels.offer");
  const tOffers = useTranslations("offerCompare.offers");
  const locale = useLocale();

  const names = [tOffers("defaultName", { letter: "A" }), tOffers("defaultName", { letter: "B" })] as const;
  const offers = exampleDrafts(locale, names).map((draft) => toOffer(draft, locale));
  const comparison = compareOffers(offers);
  const months = comparison.results[1].months;
  const scale = Math.max(1, comparison.maxMonth);

  return (
    <div className={`relative ${className}`}>
      <SampleDataBadge className="-bottom-2.5 left-3" />
      <div role="img" aria-label={t("mockAriaLabel", { name: names[1], amount: formatLira(comparison.lead, locale) })}>
        <div aria-hidden="true" className="flex flex-col gap-4">
          <OfferVerdict names={names} comparison={comparison} showNote={false} />
          <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
            <span className="text-xs text-gray-600 dark:text-gray-400">{t("monthsLabel", { name: names[1] })}</span>
            <div className="grid grid-cols-12 items-end gap-1" style={{ height: BAR_HEIGHT }}>
              {months.map((month, index) => (
                <div key={index} className="flex h-full flex-col justify-end">
                  {month.bonus > 0 && <div className="rounded-t-sm bg-accent/40" style={{ height: (month.bonus / scale) * BAR_HEIGHT }} />}
                  <div
                    className={`flex items-end justify-center bg-accent pb-1 ${month.bonus > 0 ? "" : "rounded-t-sm"}`}
                    style={{ height: ((month.net - month.bonus) / scale) * BAR_HEIGHT }}
                  >
                    <span className="text-[10px] font-semibold text-white tabular-nums dark:text-gray-950">
                      {formatNumber(month.net / 1000, locale)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
