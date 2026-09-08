"use client";

import { useTranslations } from "next-intl";
import type { CompanyGroupStatusCount } from "@/types/api";
import { STATUS_BAR_COLORS } from "@/components/applications/StatusBadge";

/**
 * How a company's applications are spread across statuses, as one bar.
 *
 * Segments are sized by share rather than drawn one-per-application, so a company with two
 * applications and one with twenty read at the same width and can be compared down the column. The
 * bar is decoration for a screen reader — the same breakdown is in the text alternative, which is
 * what "2 Mülakat, 1 Reddedildi" has to be for anyone who cannot see the colours.
 */
export function StatusDistribution({ counts }: { counts: CompanyGroupStatusCount[] }) {
  const t = useTranslations("status");

  if (counts.length === 0) {
    return null;
  }

  const total = counts.reduce((sum, entry) => sum + entry.count, 0);
  const label = counts.map((entry) => `${entry.count} ${t(entry.status)}`).join(", ");

  return (
    <span className="flex items-center gap-2">
      <span aria-hidden className="flex h-1.5 w-24 gap-0.5 overflow-hidden rounded-full">
        {counts.map((entry) => (
          <span
            key={entry.status}
            className={`h-full ${STATUS_BAR_COLORS[entry.status]}`}
            style={{ width: `${(entry.count / total) * 100}%` }}
          />
        ))}
      </span>
      <span className="truncate text-xs text-gray-500 dark:text-gray-400">{label}</span>
    </span>
  );
}
