"use client";

import { useTranslations } from "next-intl";
import type { SelectionState } from "@/lib/applications/bulkSelection";
import { selectionCount } from "@/lib/applications/bulkSelection";
import { Button } from "@/components/ui/Button";

interface BulkActionBarProps {
  selection: SelectionState;
  onClearSelection: () => void;
  onChangeStatus: () => void;
  onDelete: () => void;
}

/**
 * The action bar for a live selection. It floats over the list rather than sitting in the flow so
 * the actions stay reachable while scrolling a long list — the row you tick at the bottom of page
 * one should not require scrolling back up to act on it.
 *
 * It has to share the viewport with the app's feedback launcher (`fixed bottom-4 right-4`), and it
 * does that with geometry rather than by reaching into that component: the bar is centred and only
 * as wide as its contents, which keeps it clear of the launcher's corner on a wide screen, and below
 * `lg` it lifts to `bottom-20` so it sits above the launcher instead of on top of it.
 */
export function BulkActionBar({ selection, onClearSelection, onChangeStatus, onDelete }: BulkActionBarProps) {
  const t = useTranslations("applications.bulk");
  const count = selectionCount(selection);

  if (count === 0) {
    return null;
  }

  return (
    <div className="pointer-events-none fixed inset-x-0 bottom-20 z-30 flex justify-center px-4 lg:bottom-4">
      <div
        role="status"
        aria-live="polite"
        className="pointer-events-auto flex flex-wrap items-center justify-center gap-x-3 gap-y-2 rounded-xl bg-gray-900 px-4 py-2.5 shadow-2xl ring-1 ring-white/10 dark:bg-gray-800"
      >
        <span className="text-sm font-medium text-white">
          {selection.kind === "allMatching"
            ? t("allMatchingSelected", { count })
            : t("selectedCount", { count })}
        </span>

        <span aria-hidden className="hidden h-6 w-px bg-gray-700 sm:block dark:bg-gray-600" />

        <div className="flex items-center gap-2">
          {/* A plain button rather than <Button variant="secondary"> with an override: the variant's
              own colours and an override class have the same specificity, so which one wins comes
              down to their order in the generated stylesheet — and on this dark bar the loser reads
              as a disabled control. */}
          <button
            type="button"
            onClick={onChangeStatus}
            className="rounded-md bg-white/10 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-white/20"
          >
            {t("changeStatus")}
          </button>
          <Button variant="danger" onClick={onDelete}>
            {t("delete")}
          </Button>
          <button
            type="button"
            onClick={onClearSelection}
            aria-label={t("clearSelection")}
            className="rounded-md p-2 text-gray-400 transition-colors hover:bg-gray-800 hover:text-gray-100 dark:hover:bg-gray-700"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
      </div>
    </div>
  );
}

/**
 * The "select all N matching" offer, rendered as a strip inside the table under its header — the
 * one place where widening the selection beyond the visible page is a deliberate, separate click.
 * Also the strip that says so afterwards, since "all 247" is a claim the user should keep seeing.
 */
export function BulkSelectAllNotice({
  selection,
  totalCount,
  onSelectAllMatching,
  onClearSelection,
}: {
  selection: SelectionState;
  totalCount: number;
  onSelectAllMatching: () => void;
  onClearSelection: () => void;
}) {
  const t = useTranslations("applications.bulk");

  if (selection.kind === "allMatching") {
    return (
      <div className="flex flex-wrap items-center justify-center gap-x-2 gap-y-1 bg-warn-wash px-4 py-2.5 text-sm text-warn-ink">
        <span>{t("allMatchingSelected", { count: selection.countWhenSelected })}</span>
        <button type="button" onClick={onClearSelection} className="font-medium underline underline-offset-2">
          {t("clearSelection")}
        </button>
      </div>
    );
  }

  return (
    <div className="flex flex-wrap items-center justify-center gap-x-2 gap-y-1 bg-accent-wash px-4 py-2.5 text-sm text-gray-800 dark:text-gray-200">
      <span>{t("pageSelected", { count: selection.ids.length })}</span>
      <button
        type="button"
        onClick={onSelectAllMatching}
        className="font-medium text-accent-ink underline underline-offset-2"
      >
        {t("selectAllMatching", { count: totalCount })}
      </button>
    </div>
  );
}
