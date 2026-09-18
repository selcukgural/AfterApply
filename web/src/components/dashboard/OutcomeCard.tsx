"use client";

import { useLocale, useTranslations } from "next-intl";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { formatCount, formatRate } from "@/lib/dashboard/format";
import { summariseOutcome, TONE_CHIP, TONE_FILL, type Tone } from "@/lib/dashboard/statusGroups";
import type { StatusDistributionItem } from "@/types/api";

export function OutcomeCard({ distribution }: { distribution: StatusDistributionItem[] }) {
  const t = useTranslations("dashboard.outcome");
  const locale = useLocale();
  const outcome = summariseOutcome(distribution);

  // Everything that is not a positive outcome is neutral — a rejection is a result, not an
  // alarm (T1). The three neutral segments still have to be told apart on the bar, so they step
  // down in opacity in the order the legend lists them; the chips carry the labels.
  const segments: { key: string; count: number; tone: Tone; fill: string }[] = [
    { key: "won", count: outcome.won, tone: "good", fill: TONE_FILL.good },
    { key: "lost", count: outcome.lost, tone: "muted", fill: TONE_FILL.muted },
    { key: "noAnswer", count: outcome.noAnswer, tone: "muted", fill: `${TONE_FILL.muted} opacity-60` },
    { key: "withdrawn", count: outcome.withdrawn, tone: "muted", fill: `${TONE_FILL.muted} opacity-35` },
  ];

  if (outcome.resolved === 0) {
    return (
      <Card>
        <CardHeader title={t("title")} />
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col">
      <CardHeader title={t("title")} hint={t("resolved", { count: outcome.resolved })} />

      {/* 2px gaps in the surface colour separate the segments — no strokes, which would add ink
          that isn't data. */}
      <div className="mb-3 flex h-3 gap-0.5 overflow-hidden rounded-full">
        {segments
          .filter((segment) => segment.count > 0)
          .map((segment) => (
            <span
              key={segment.key}
              className={segment.fill}
              style={{ flex: `${segment.count} 0 0` }}
            />
          ))}
      </div>

      <dl className="flex flex-col">
        {segments.map((segment) => (
          <div
            key={segment.key}
            className="flex items-center justify-between gap-3 border-t border-gray-100 py-1.5 first:border-t-0 dark:border-gray-800/60"
          >
            <dt className={`w-fit rounded-full px-2 py-0.5 text-xs font-medium ${TONE_CHIP[segment.tone]}`}>
              {t(segment.key)}
            </dt>
            <dd className="text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100">
              {formatCount(segment.count, locale)}
            </dd>
          </div>
        ))}
      </dl>

      <p className="mt-auto border-t border-gray-100 pt-3 text-sm text-gray-600 dark:border-gray-800/60 dark:text-gray-400">
        {t("winRate")}{" "}
        <b className="font-semibold text-good-ink">{formatRate(outcome.winRate, locale)}</b>
      </p>
    </Card>
  );
}
