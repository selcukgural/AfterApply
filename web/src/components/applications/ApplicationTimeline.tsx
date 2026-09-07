"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { ApplicationEventResponse, ApplicationStatusHistoryResponse } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { OriginChip } from "@/components/applications/OriginChip";
import { hasEmailContext, hasStatedRejectionReason } from "@/lib/applications/statusHistory";
import { mergeTimeline, readEventNote } from "@/lib/applications/timeline";

/**
 * The application's history as one list: status changes the product recorded, and events the user
 * added by hand. They are separate resources on the API and were separate ideas in the code, but to
 * the person reading the page they are one story — two stacked lists would make the reader do the
 * interleaving themselves.
 */
export function ApplicationTimeline({
  history,
  events,
}: {
  history: ApplicationStatusHistoryResponse[];
  events: ApplicationEventResponse[];
}) {
  const t = useTranslations("applications.statusHistory");
  const tReason = useTranslations("emailSuggestions.rejectionReasonCategory");
  const tEventType = useTranslations("applicationEventType");
  const locale = useLocale();
  const [openEmailFor, setOpenEmailFor] = useState<string | null>(null);

  const items = mergeTimeline(history, events);

  if (items.length === 0) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

  return (
    <ol className="flex flex-col">
      {items.map((item) => (
        <li
          key={`${item.kind}-${item.id}`}
          className="flex flex-col gap-2 border-t border-gray-200 py-3 first:border-t-0 first:pt-0 dark:border-gray-800 sm:flex-row sm:gap-4"
        >
          <time
            dateTime={item.at}
            className="shrink-0 text-xs tabular-nums text-gray-500 dark:text-gray-400 sm:w-32"
          >
            {new Date(item.at).toLocaleString(locale, { dateStyle: "medium", timeStyle: "short" })}
          </time>

          <div className="flex min-w-0 flex-1 flex-col gap-2">
            {item.kind === "status" ? (
              <StatusRow
                entry={item.entry}
                isEmailOpen={openEmailFor === item.entry.id}
                onToggleEmail={() => setOpenEmailFor(openEmailFor === item.entry.id ? null : item.entry.id)}
                t={t}
                tReason={tReason}
              />
            ) : (
              <>
                <span className="w-fit rounded-full bg-gray-100 px-2 py-0.5 text-xs font-medium text-gray-700 dark:bg-gray-800 dark:text-gray-300">
                  {tEventType(item.event.type)}
                </span>
                {readEventNote(item.event.metadata) && (
                  // Rendered as text, never as markup: this is whatever the user typed.
                  <p className="whitespace-pre-wrap border-l-2 border-gray-200 pl-2.5 text-sm text-gray-900 dark:border-gray-700 dark:text-gray-100">
                    {readEventNote(item.event.metadata)}
                  </p>
                )}
              </>
            )}
          </div>
        </li>
      ))}
    </ol>
  );
}

/** Unchanged from the old StatusHistoryList — same markup, just addressable per row now. */
function StatusRow({
  entry,
  isEmailOpen,
  onToggleEmail,
  t,
  tReason,
}: {
  entry: ApplicationStatusHistoryResponse;
  isEmailOpen: boolean;
  onToggleEmail: () => void;
  t: ReturnType<typeof useTranslations>;
  tReason: ReturnType<typeof useTranslations>;
}) {
  const hasReason = hasStatedRejectionReason(entry);
  const hasEmail = hasEmailContext(entry);

  return (
    <>
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
          <span className="text-gray-900 dark:text-gray-100">{tReason(entry.rejectionReasonCategory!)}</span>
          {entry.rejectionReasonDetail && (
            <span className="text-gray-500 dark:text-gray-400"> — {entry.rejectionReasonDetail}</span>
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
            onClick={onToggleEmail}
            aria-expanded={isEmailOpen}
            className="rounded text-xs text-blue-600 hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-600 dark:text-blue-400"
          >
            {isEmailOpen ? t("hideEmail") : t("showEmail")}
          </button>
          {isEmailOpen && (
            <div className="mt-1.5 rounded-md bg-gray-50 p-2.5 dark:bg-gray-800">
              {entry.emailSubject && (
                <p className="text-xs font-medium text-gray-900 dark:text-gray-100">{entry.emailSubject}</p>
              )}
              {entry.emailSnippet && (
                <p className="mt-1 text-xs text-gray-600 dark:text-gray-400">{entry.emailSnippet}</p>
              )}
            </div>
          )}
        </div>
      )}
    </>
  );
}
