"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { ApplicationStatusHistoryResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { OriginChip } from "@/components/applications/OriginChip";
import { hasEmailContext, hasStatedRejectionReason } from "@/lib/applications/statusHistory";

export function StatusHistoryList({ history }: { history: ApplicationStatusHistoryResponse[] }) {
  const t = useTranslations("applications.statusHistory");
  const tReason = useTranslations("emailSuggestions.rejectionReasonCategory");
  const locale = useLocale();
  const [openEmailFor, setOpenEmailFor] = useState<string | null>(null);

  if (history.length === 0) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

  return (
    <ol className="flex flex-col">
      {history.map((entry) => {
        const hasReason = hasStatedRejectionReason(entry);
        const hasEmail = hasEmailContext(entry);
        const isEmailOpen = openEmailFor === entry.id;

        return (
          <li
            key={entry.id}
            className="flex flex-col gap-2 border-t border-gray-200 py-3 first:border-t-0 first:pt-0 dark:border-gray-800 sm:flex-row sm:gap-4"
          >
            <time
              dateTime={entry.changedAt}
              className="shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-400 sm:w-32"
            >
              {new Date(entry.changedAt).toLocaleString(locale, {
                dateStyle: "medium",
                timeStyle: "short",
              })}
            </time>

            <div className="flex min-w-0 flex-1 flex-col gap-2">
              <div className="flex flex-wrap items-center gap-1.5">
                {entry.fromStatus && (
                  <>
                    <StatusBadge status={entry.fromStatus} />
                    <span aria-hidden className="text-xs text-gray-400 dark:text-gray-500">
                      →
                    </span>
                  </>
                )}
                <StatusBadge status={entry.toStatus} />
                <OriginChip origin={entry.origin} />
              </div>

              {hasReason && (
                <p className="text-xs text-gray-600 dark:text-gray-400">
                  {t("rejectionReason")}{" "}
                  <span className="text-gray-900 dark:text-gray-100">
                    {tReason(entry.rejectionReasonCategory!)}
                  </span>
                  {entry.rejectionReasonDetail && (
                    <span className="text-gray-500 dark:text-gray-400">
                      {" "}
                      — {entry.rejectionReasonDetail}
                    </span>
                  )}
                </p>
              )}

              {entry.note && (
                <p className="whitespace-pre-wrap border-l-2 border-gray-200 pl-2.5 text-sm text-gray-900 dark:border-gray-700 dark:text-gray-100">
                  {entry.note}
                </p>
              )}

              {hasEmail && (
                <div>
                  <button
                    type="button"
                    onClick={() => setOpenEmailFor(isEmailOpen ? null : entry.id)}
                    aria-expanded={isEmailOpen}
                    className="rounded text-xs text-blue-600 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:text-blue-400"
                  >
                    {isEmailOpen ? t("hideEmail") : t("showEmail")}
                  </button>
                  {isEmailOpen && (
                    <div className="mt-1.5 rounded-md bg-gray-50 p-2.5 dark:bg-gray-800">
                      {entry.emailSubject && (
                        <p className="text-xs font-medium text-gray-900 dark:text-gray-100">
                          {entry.emailSubject}
                        </p>
                      )}
                      {entry.emailSnippet && (
                        <p className="mt-1 text-xs text-gray-600 dark:text-gray-400">{entry.emailSnippet}</p>
                      )}
                    </div>
                  )}
                </div>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
