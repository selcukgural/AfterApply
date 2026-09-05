"use client";

import { useLocale, useTranslations } from "next-intl";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { buildFunnel, findBottleneck } from "@/lib/dashboard/funnel";
import type { AnalyticsRatesResponse } from "@/types/api";

export function ConversionFunnel({ rates }: { rates: AnalyticsRatesResponse }) {
  const t = useTranslations("dashboard.funnel");
  const locale = useLocale();

  const stages = buildFunnel(rates);
  const bottleneck = findBottleneck(stages);

  return (
    <Card className="flex flex-col">
      <CardHeader title={t("title")} hint={t("hint")} />

      <ol className="flex flex-col">
        {stages.map((stage) => {
          // The first stage is the baseline (always a full rail); later stages can compute above
          // 100% when an application skips a step, so the drawn width is clamped, not the label.
          const fill = stage.conversion === null ? 100 : Math.min(100, stage.conversion);
          const isOffer = stage.key === "offer";

          return (
            <li
              key={stage.key}
              className="grid grid-cols-[minmax(5.5rem,auto)_1fr_minmax(5.5rem,auto)] items-center gap-3 border-t border-gray-100 py-2.5 first:border-t-0 dark:border-gray-800/60"
            >
              <span className="text-sm text-gray-800 dark:text-gray-200">{t(stage.key)}</span>
              <span className="h-2.5 overflow-hidden rounded-full bg-track">
                {/* A sub-1% stage would otherwise round to nothing on screen and read as a
                    rendering bug. The floor keeps "almost none got through" visible as a mark. */}
                <span
                  className={`block h-full rounded-full ${isOffer ? "bg-good" : "bg-accent"}`}
                  style={{ width: `${fill}%`, minWidth: stage.count > 0 ? "0.375rem" : undefined }}
                />
              </span>
              <span className="text-right text-sm text-gray-500 tabular-nums dark:text-gray-400">
                <b className="font-semibold text-gray-900 dark:text-gray-100">
                  {formatCount(stage.count, locale)}
                </b>
                {stage.conversion !== null ? ` · ${formatRate(stage.conversion, locale)}` : null}
              </span>
            </li>
          );
        })}
      </ol>

      {/* mt-auto: rows share a height, so the closing line sits on the card's floor rather than
          leaving a gap beneath it. */}
      {bottleneck ? (
        <p className="mt-auto border-t border-gray-100 pt-3 text-sm text-gray-600 dark:border-gray-800/60 dark:text-gray-400">
          {t.rich("bottleneck", {
            stage: t(bottleneck.key),
            rate: formatRate(bottleneck.conversion!, locale),
            strong: (chunks) => <b className="font-semibold text-gray-900 dark:text-gray-100">{chunks}</b>,
          })}
        </p>
      ) : null}
    </Card>
  );
}
