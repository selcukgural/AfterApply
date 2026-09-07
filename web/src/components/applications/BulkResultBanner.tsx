"use client";

import { useTranslations } from "next-intl";
import type { BulkStatusChange } from "@/types/api";
import { Button } from "@/components/ui/Button";

/**
 * What happened, and — for a status change — the way back out of it.
 *
 * `changes` is what the server reported it actually moved, so the undo puts back exactly that and
 * nothing else. A delete has no `changes` and no undo: the rows are gone for good, which is what the
 * confirmation said they would be.
 */
export type BulkResult =
  | { kind: "statusChanged"; updated: number; skipped: number; changes: BulkStatusChange[] }
  | { kind: "statusUndone"; reverted: number; skipped: number }
  | { kind: "deleted"; deleted: number };

interface BulkResultBannerProps {
  result: BulkResult;
  isUndoing: boolean;
  undoError: string | null;
  onUndo: () => void;
  onDismiss: () => void;
}

export function BulkResultBanner({ result, isUndoing, undoError, onUndo, onDismiss }: BulkResultBannerProps) {
  const t = useTranslations("applications.bulk.result");

  // Sits in the page flow where the selection toolbar was, rather than as a floating toast: the
  // undo must not be something a user loses by looking away, and this app has no toast layer.
  return (
    <div
      // Announced because the banner replaces the toolbar the user was just looking at, so nothing
      // else on screen reports the outcome.
      role="status"
      aria-live="polite"
      className="flex flex-wrap items-center gap-x-3 gap-y-2 rounded-lg border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900"
    >
      <span className="text-sm text-gray-900 dark:text-gray-100">
        {result.kind === "statusChanged" && t("statusChanged", { count: result.updated })}
        {result.kind === "statusUndone" && t("statusUndone", { count: result.reverted })}
        {result.kind === "deleted" && t("deleted", { count: result.deleted })}
      </span>

      {result.kind === "statusChanged" && result.skipped > 0 && (
        <span className="text-sm text-gray-500 dark:text-gray-400">{t("skippedAlreadyInStatus", { count: result.skipped })}</span>
      )}
      {result.kind === "statusUndone" && result.skipped > 0 && (
        // The rows that moved on between the change and the undo — a decision made after the one
        // being undone always wins.
        <span className="text-sm text-gray-500 dark:text-gray-400">{t("skippedMovedOn", { count: result.skipped })}</span>
      )}

      {undoError && <span className="text-sm text-red-600 dark:text-red-400">{undoError}</span>}

      <span className="flex-1" />

      {result.kind === "statusChanged" && result.changes.length > 0 && (
        <Button variant="secondary" onClick={onUndo} disabled={isUndoing}>
          {isUndoing ? t("undoing") : t("undo")}
        </Button>
      )}

      <button
        type="button"
        onClick={onDismiss}
        aria-label={t("dismiss")}
        className="rounded p-1.5 text-gray-500 hover:bg-gray-100 hover:text-gray-700 dark:text-gray-400 dark:hover:bg-gray-800 dark:hover:text-gray-200"
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
          <line x1="18" y1="6" x2="6" y2="18" />
          <line x1="6" y1="6" x2="18" y2="18" />
        </svg>
      </button>
    </div>
  );
}
