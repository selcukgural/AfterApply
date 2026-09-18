"use client";

import { type ReactNode, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { type BulkResult, BulkResultBanner } from "@/components/applications/BulkResultBanner";
import { Button } from "@/components/ui/Button";
import { useAcknowledgeBreak, useCloseSilenced, useReminderBreak, useUndoCloseSilenced } from "@/hooks/useReminderBreak";
import { toUndoEntries } from "@/lib/applications/bulkSelection";
import { formatCount } from "@/lib/dashboard/format";
import { ApiError } from "@/lib/api/httpClient";

/**
 * Stands between the dashboard and the two things a break hides — the reminders card and the
 * stale-applications question (DEVELOPMENT_PLAN.md, T-series, T5).
 *
 * No break: the children render as they always did. Break running: one muted line says so and
 * where to end it; nothing else. Break over: the children stay hidden behind a single question —
 * "N went quiet while you were away, close them all?" — until it is answered, so the return is
 * one decision, not a backlog. Someone who has been searching for months and took a week off
 * must not be met by twelve reminders the moment they come back.
 */
export function ReminderBreakGate({ children }: { children: ReactNode }) {
  const t = useTranslations("dashboard.break");
  const locale = useLocale();
  const { data: pause } = useReminderBreak();
  const acknowledge = useAcknowledgeBreak();
  const closeSilenced = useCloseSilenced();
  const undo = useUndoCloseSilenced();
  const [result, setResult] = useState<BulkResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [undoError, setUndoError] = useState<string | null>(null);

  const describeError = (e: unknown) => (e instanceof ApiError ? e.message : t("error"));

  // The strip after "yes": the same undo the reminders card offers, and it outlives the question.
  if (result) {
    return (
      <BulkResultBanner
        result={result}
        isUndoing={undo.isPending}
        undoError={undoError}
        onUndo={() => {
          if (result.kind !== "statusChanged") return;
          undo.mutate(toUndoEntries(result.changes), {
            onSuccess: (response) => {
              setResult({ kind: "statusUndone", reverted: response.reverted, skipped: response.skipped });
              setUndoError(null);
            },
            onError: (e) => setUndoError(describeError(e)),
          });
        }}
        onDismiss={() => {
          setResult(null);
          setUndoError(null);
        }}
      />
    );
  }

  // Unknown yet (first load) or no break: the board as usual. Unknown resolves within a request
  // and a break is the rare case, so drawing the children first is the right default.
  if (!pause || pause.state === "None") return <>{children}</>;

  if (pause.state === "Paused") {
    const until = new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(new Date(pause.pausedUntil!));
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400">
        {t("paused", { date: until })}{" "}
        <Link href="/profile" className="font-medium text-gray-700 underline-offset-2 hover:underline dark:text-gray-300">
          {t("end")}
        </Link>
      </p>
    );
  }

  const count = pause.silencedCount;
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex flex-wrap items-center gap-x-4 gap-y-2 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900"
    >
      <p className="flex-1 text-sm text-gray-800 dark:text-gray-200">
        {count > 0 ? t("returnedSome", { count: formatCount(count, locale) }) : t("returnedNone")}
      </p>
      {error ? <span className="text-sm text-red-600 dark:text-red-400">{error}</span> : null}
      <div className="flex items-center gap-2">
        {count > 0 ? (
          <>
            <Button
              variant="primary"
              className="px-3 py-1.5 text-sm"
              disabled={closeSilenced.isPending || acknowledge.isPending}
              onClick={() =>
                closeSilenced.mutate(undefined, {
                  onSuccess: (response) => {
                    setError(null);
                    setResult({
                      kind: "statusChanged",
                      updated: response.updated,
                      skipped: response.skippedAlreadyInStatus,
                      changes: response.changes,
                    });
                  },
                  onError: (e) => setError(describeError(e)),
                })
              }
            >
              {closeSilenced.isPending ? t("closing") : t("close", { count: formatCount(count, locale) })}
            </Button>
            <Button
              variant="secondary"
              className="px-3 py-1.5 text-sm"
              disabled={closeSilenced.isPending || acknowledge.isPending}
              onClick={() => acknowledge.mutate(undefined, { onError: (e) => setError(describeError(e)) })}
            >
              {t("notNow")}
            </Button>
          </>
        ) : (
          <Button
            variant="secondary"
            className="px-3 py-1.5 text-sm"
            disabled={acknowledge.isPending}
            onClick={() => acknowledge.mutate(undefined, { onError: (e) => setError(describeError(e)) })}
          >
            {t("ok")}
          </Button>
        )}
      </div>
    </div>
  );
}
