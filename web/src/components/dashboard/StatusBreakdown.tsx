"use client";

import { useLocale, useTranslations } from "next-intl";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { formatCount } from "@/lib/dashboard/format";
import { splitDistribution, STATUS_TONE, TONE_FILL } from "@/lib/dashboard/statusGroups";
import type { StatusDistributionItem } from "@/types/api";

/**
 * Replaces the old recharts column chart, which was unreadable for anyone with a bulk import:
 * one 1,177-tall bar and nine bars a pixel high, with the status names rotated 35° underneath.
 *
 * Horizontal bars need no rotated labels, and a bucket that would flatten all the others is
 * pulled out of the shared scale instead of squashing them (see splitDistribution).
 */
export function StatusBreakdown({ data }: { data: StatusDistributionItem[] }) {
  const t = useTranslations("dashboard.breakdown");
  const tStatus = useTranslations("status");
  const locale = useLocale();

  const { dominant, rest, restMax } = splitDistribution(data);

  if (!dominant && rest.length === 0) {
    return (
      <Card>
        <CardHeader title={t("title")} />
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>
      </Card>
    );
  }

  return (
    <Card className="flex flex-col">
      <CardHeader title={t("title")} hint={dominant ? t("hint") : undefined} />

      {dominant ? (
        <div className="mb-3 flex items-center gap-3">
          <span className="w-28 shrink-0 text-sm text-gray-800 dark:text-gray-200">
            {tStatus(dominant.status)}
          </span>
          <span className="h-5 flex-1 overflow-hidden rounded-md bg-track">
            <span className={`block h-full rounded-md ${TONE_FILL[STATUS_TONE[dominant.status]]}`} />
          </span>
          <b className="w-14 shrink-0 text-right text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100">
            {formatCount(dominant.count, locale)}
          </b>
        </div>
      ) : null}

      <div className={dominant ? "border-t border-dashed border-gray-200 pt-3 dark:border-gray-700" : ""}>
        {dominant ? (
          <p className="mb-2 text-xs text-gray-500 dark:text-gray-400">
            {t("restLabel", { count: rest.reduce((sum, item) => sum + item.count, 0), max: restMax })}
          </p>
        ) : null}

        <ul className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
          {rest.map((item) => (
            <li key={item.status} className="flex items-center gap-3">
              <span className="min-w-0 flex-1 truncate text-sm text-gray-600 dark:text-gray-400">
                {tStatus(item.status)}
              </span>
              <span className="h-1.5 w-14 shrink-0 overflow-hidden rounded-full bg-track">
                <span
                  className={`block h-full rounded-full ${TONE_FILL[STATUS_TONE[item.status]]}`}
                  style={{ width: `${restMax > 0 ? (100 * item.count) / restMax : 0}%` }}
                />
              </span>
              <b className="w-8 shrink-0 text-right text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100">
                {formatCount(item.count, locale)}
              </b>
            </li>
          ))}
        </ul>
      </div>
    </Card>
  );
}
