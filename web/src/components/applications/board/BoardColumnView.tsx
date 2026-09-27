"use client";

import { useEffect, useRef } from "react";
import { useDroppable } from "@dnd-kit/core";
import { SortableContext, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ColumnState } from "@/lib/board/board";
import { type CardActions, SortableBoardCard } from "./BoardCard";

interface BoardColumnViewProps {
  state: ColumnState;
  now: Date;
  silentDays: number;
  closedVisibleDays: number;
  actions: CardActions;
  isLoadingMore: boolean;
  onLoadMore: () => void;
  /** True while a card is dragged over this column from another one. */
  isDropTarget: boolean;
  /** Hides the heading — the phone layout names the column in its tab strip instead. */
  bare?: boolean;
}

export const COLUMN_DROP_PREFIX = "column:";

/**
 * One column: its heading and count, its cards in the user's order, and a sentinel at the bottom
 * that asks for the next ten when it scrolls into view. The column itself is a drop target, so an
 * empty column (or the space under the last card) still takes a card.
 */
export function BoardColumnView({
  state,
  now,
  silentDays,
  closedVisibleDays,
  actions,
  isLoadingMore,
  onLoadMore,
  isDropTarget,
  bare = false,
}: BoardColumnViewProps) {
  const t = useTranslations("applications.board");
  const { setNodeRef } = useDroppable({ id: `${COLUMN_DROP_PREFIX}${state.column}`, data: { column: state.column } });
  const scrollRef = useRef<HTMLDivElement>(null);
  const sentinelRef = useRef<HTMLDivElement>(null);
  const hasMore = state.nextCursor !== null;

  // Scrolling to the bottom of a column loads its next page — per column, so a long "Applied"
  // never waits on the others. The observer watches the column's own scroll box.
  useEffect(() => {
    const sentinel = sentinelRef.current;
    if (!sentinel || !hasMore) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting) && !isLoadingMore) onLoadMore();
      },
      { root: scrollRef.current, rootMargin: "120px" },
    );
    observer.observe(sentinel);
    return () => observer.disconnect();
  }, [hasMore, isLoadingMore, onLoadMore]);

  // Each column's "+" opens the form already in that column's stage (In progress starts at its
  // first stage; the form lets the user pick another) and comes back to the board after saving.
  const addStatus = { Applied: "Applied", InProgress: "Screening", Offer: "Offer" } as const;
  const addHref =
    state.column === "Saved"
      ? "/tracked-jobs"
      : state.column === "Closed"
        ? null
        : `/applications/new?from=board&status=${addStatus[state.column]}`;

  return (
    <section
      ref={setNodeRef}
      aria-label={t(`columns.${state.column}`)}
      // h-full: the column must be exactly as tall as its slot so its card list scrolls inside it.
      // Taller than the slot, the list never scrolls, the "load more" sentinel is always in view,
      // and every page loads at once (seen on the phone layout, 2026-09-27).
      className={`flex h-full min-h-0 min-w-0 flex-col gap-2 rounded-xl p-2.5 transition-colors ${
        state.column === "Closed" ? "bg-gray-200/70 dark:bg-gray-800/70" : "bg-muted-wash dark:bg-gray-800/50"
      } ${isDropTarget ? "ring-2 ring-accent/60" : ""}`}
    >
      {!bare && (
        <header className="flex items-center justify-between px-1 pt-0.5">
          <h2 className="text-[13px] font-semibold text-gray-700 dark:text-gray-200">{t(`columns.${state.column}`)}</h2>
          <span className="rounded-full bg-white px-2 py-0.5 text-xs text-gray-600 dark:bg-gray-900 dark:text-gray-300">
            {state.total}
          </span>
        </header>
      )}

      {addHref && (
        <Link
          href={addHref}
          className="rounded-lg border border-dashed border-gray-300 py-1.5 text-center text-[13px] text-gray-600 hover:border-accent hover:text-accent-ink dark:border-gray-700 dark:text-gray-400"
        >
          + {state.column === "Saved" ? t("addSaved") : t("addApplication")}
        </Link>
      )}
      {state.column === "Closed" && (
        <p className="px-1 text-[11px] leading-snug text-gray-600 dark:text-gray-400">
          {t("closedNote", { days: closedVisibleDays })}
        </p>
      )}

      <div ref={scrollRef} className="-mx-1 flex min-h-16 flex-1 flex-col gap-2 overflow-y-auto px-1 pb-1">
        <SortableContext items={state.cards.map((card) => card.id)} strategy={verticalListSortingStrategy}>
          {state.cards.map((card) => (
            <SortableBoardCard key={card.id} card={card} now={now} silentDays={silentDays} actions={actions} />
          ))}
        </SortableContext>
        {state.cards.length === 0 && (
          <p className="py-6 text-center text-xs text-gray-500 dark:text-gray-400">{t("emptyColumn")}</p>
        )}
        {hasMore && (
          <div ref={sentinelRef} className="flex flex-col items-center gap-1 py-2">
            <button
              type="button"
              onClick={onLoadMore}
              disabled={isLoadingMore}
              className="rounded-md px-3 py-1.5 text-xs font-medium text-accent-ink hover:bg-white disabled:text-gray-400 dark:hover:bg-gray-900"
            >
              {isLoadingMore ? t("loading") : t("loadMore")}
            </button>
          </div>
        )}
      </div>
      {state.cards.length > 0 && (
        <p className="text-center text-[11px] text-gray-500 dark:text-gray-400" aria-live="polite">
          {t("loaded", { shown: state.cards.length, total: state.total })}
        </p>
      )}
    </section>
  );
}
