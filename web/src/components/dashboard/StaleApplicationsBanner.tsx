"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";
import { type BulkResult, BulkResultBanner } from "@/components/applications/BulkResultBanner";
import { useDismissStaleSuggestion, useGhostStale, useStaleSummary, useUndoStaleGhost } from "@/hooks/useStaleApplications";
import { ApiError } from "@/lib/api/httpClient";
import { formatCount } from "@/lib/dashboard/format";

/**
 * The one question an old import raises, asked once: "these N applications are past the horizon
 * and unanswered — mark them all as ghosted?" Before 2026-09-13 the same import produced one
 * reminder row per application (1,225 of them, each with its own "dismiss"), which is not a
 * reminder, it is a chore. The scan no longer creates reminders past the horizon; this is what
 * replaces them.
 *
 * Three states, in order: the question; the result strip with its undo (the Applications page's
 * own, so the way back looks the same everywhere); nothing, once the user answered "not now" — the
 * server keeps that answer and the reminders card carries a one-line pointer to the bulk tools.
 */
export function StaleApplicationsBanner() {
  const t = useTranslations("dashboard.stale");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const { data: summary } = useStaleSummary();
  const ghost = useGhostStale();
  const undo = useUndoStaleGhost();
  const dismiss = useDismissStaleSuggestion();
  const [result, setResult] = useState<BulkResult | null>(null);
  const [undoError, setUndoError] = useState<string | null>(null);

  const describeError = (error: unknown) => (error instanceof ApiError ? error.message : tErrors("generic"));

  // The result strip outlives the question that produced it: once the batch has moved, the
  // summary count is 0 and the question is gone, but the undo has to stay until it is closed.
  if (result) {
    return (
      <BulkResultBanner
        result={result}
        isUndoing={undo.isPending}
        undoError={undoError}
        onUndo={() => {
          if (result.kind !== "statusChanged") return;
          undo.mutate(result.changes, {
            onSuccess: (response) => {
              setResult({ kind: "statusUndone", reverted: response.reverted, skipped: response.skipped });
              setUndoError(null);
            },
            onError: (error) => setUndoError(describeError(error)),
          });
        }}
        onDismiss={() => {
          setResult(null);
          setUndoError(null);
        }}
      />
    );
  }

  if (!summary || !summary.suggest) return null;

  const count = summary.count;

  return (
    <div
      role="region"
      aria-label={t("title", { count, threshold: summary.thresholdDays })}
      className="flex flex-wrap items-center justify-between gap-x-6 gap-y-3 rounded-xl border border-accent/35 bg-accent-wash px-5 py-4"
    >
      <div className="flex min-w-0 flex-col gap-1">
        <p className="text-sm font-semibold text-gray-900 dark:text-gray-100">
          {t("title", { count, threshold: summary.thresholdDays })}
        </p>
        <p className="max-w-[70ch] text-[13px] leading-[18px] text-gray-700 dark:text-gray-300">
          {t("body", { oldest: formatCount(summary.oldestDays, locale) })}
        </p>
        {ghost.isError ? <p className="text-xs text-red-600 dark:text-red-400">{t("error")}</p> : null}
      </div>
      <div className="flex shrink-0 items-center gap-2">
        <Button
          type="button"
          variant="primary"
          disabled={ghost.isPending || dismiss.isPending}
          onClick={() =>
            ghost.mutate(undefined, {
              onSuccess: (response) =>
                setResult({
                  kind: "statusChanged",
                  updated: response.updated,
                  skipped: response.skippedAlreadyInStatus,
                  changes: response.changes,
                }),
            })
          }
        >
          {ghost.isPending ? t("confirming") : t("confirm", { count })}
        </Button>
        <Button
          type="button"
          variant="secondary"
          disabled={ghost.isPending || dismiss.isPending}
          onClick={() => dismiss.mutate()}
        >
          {t("notNow")}
        </Button>
      </div>
    </div>
  );
}
