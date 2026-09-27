"use client";

import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState, useSyncExternalStore } from "react";
import {
  closestCorners,
  type CollisionDetection,
  DndContext,
  type DragEndEvent,
  type DragOverEvent,
  DragOverlay,
  type DragStartEvent,
  KeyboardSensor,
  MouseSensor,
  pointerWithin,
  TouchSensor,
  useSensor,
  useSensors,
} from "@dnd-kit/core";
import { sortableKeyboardCoordinates } from "@dnd-kit/sortable";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import { boardApi } from "@/lib/api/board";
import {
  activeFilterCount,
  appendPage,
  applyMove,
  type BoardFilterState,
  boardFilterParams,
  canDrop,
  columnFor,
  findCard,
  neighboursOf,
  parseBoardFilter,
  removeCard,
  statusesForColumn,
  statusForDrop,
  toBoardFilter,
  toColumnStates,
  type ColumnState,
  BOARD_COLUMNS,
} from "@/lib/board/board";
import type { ApplicationStatus, BoardCardResponse, BoardColumn, BoardFilter } from "@/types/api";
import { AddToBoardDialog } from "./AddToBoardDialog";
import { BoardCardBody, type CardActions } from "./BoardCard";
import { BoardColumnView, COLUMN_DROP_PREFIX } from "./BoardColumnView";
import { BoardFilterDrawer } from "./BoardFilterDrawer";
import { StagePicker } from "./StagePicker";

const DESKTOP_QUERY = "(min-width: 768px)";

function subscribeDesktop(callback: () => void) {
  const query = window.matchMedia(DESKTOP_QUERY);
  query.addEventListener("change", callback);
  return () => query.removeEventListener("change", callback);
}

/** Five columns side by side from md up; one column at a time behind a tab strip below it. Chosen
 *  in script rather than CSS because each card may be registered with the drag layer only once. */
function useIsDesktop(): boolean {
  return useSyncExternalStore(
    subscribeDesktop,
    () => window.matchMedia(DESKTOP_QUERY).matches,
    () => true,
  );
}

interface PendingStage {
  card: BoardCardResponse;
  target: BoardColumn | null;
  index: number;
  statuses: readonly ApplicationStatus[];
}

/**
 * The applications board (DECISIONS.md 2026-09-27): five columns of the user's own cards, ten per
 * column at a time with more on scroll, moved by drag or by each card's menu, filtered through a
 * drawer whose state lives in the URL. What is on the board is the server's; this component shows
 * a move at once and puts the board back from the server if the move is refused.
 */
export function BoardView({ toggle }: { toggle: ReactNode }) {
  const t = useTranslations("applications.board");
  const tDnd = useTranslations("applications.board.dnd");
  const tColumns = useTranslations("applications.board.columns");
  const tSource = useTranslations("applications.board.source");
  const tDrawer = useTranslations("applications.board.drawer");
  const tChips = useTranslations("applications.board.chips");
  const router = useRouter();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const isDesktop = useIsDesktop();

  const filterState = useMemo(() => parseBoardFilter(new URLSearchParams(searchParams.toString())), [searchParams]);
  const [now, setNow] = useState(() => new Date());
  /** The exact filter the loaded pages were read with, so "load more" asks for the same set. */
  const requestFilter = useRef<BoardFilter>(toBoardFilter(filterState, now));

  const boardQuery = useQuery({
    queryKey: ["board", filterState],
    // Never served from cache. The board changes under the user (e-mail, the extension) and in
    // their hands (a move updates the local columns, not this cache), so a copy kept for the app's
    // usual 30 s would, on switching a filter off again, show a board from before the last move.
    staleTime: 0,
    gcTime: 0,
    queryFn: () => {
      const at = new Date();
      requestFilter.current = toBoardFilter(filterState, at);
      setNow(at);
      return boardApi.get(requestFilter.current);
    },
  });

  // Local copies of what the server sent, so moves show at once and extra pages append in place.
  // Reset whenever a fresh board arrives — during render, not in an effect, which is React's own
  // pattern for state derived from a changing input.
  const [columns, setColumns] = useState<ColumnState[] | null>(null);
  const [unseenCount, setUnseenCount] = useState(0);
  const [syncedData, setSyncedData] = useState<typeof boardQuery.data>(undefined);
  if (boardQuery.data && boardQuery.data !== syncedData) {
    setSyncedData(boardQuery.data);
    setColumns(toColumnStates(boardQuery.data.columns));
    setUnseenCount(boardQuery.data.unseenCount);
  }

  const silentDays = boardQuery.data?.silentDays ?? 14;
  const closedVisibleDays = boardQuery.data?.closedVisibleDays ?? 14;

  const [error, setError] = useState<string | null>(null);
  const [loadingColumns, setLoadingColumns] = useState<BoardColumn[]>([]);
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [addOpen, setAddOpen] = useState(false);
  const [adding, setAdding] = useState(false);
  const [addError, setAddError] = useState<string | null>(null);
  const [pendingStage, setPendingStage] = useState<PendingStage | null>(null);
  const [activeCard, setActiveCard] = useState<BoardCardResponse | null>(null);
  const [overColumn, setOverColumn] = useState<BoardColumn | null>(null);
  const [mobileColumn, setMobileColumn] = useState<BoardColumn>("Applied");
  const [searchInput, setSearchInput] = useState(filterState.search);

  const updateFilter = useCallback(
    (next: BoardFilterState) => {
      const params = new URLSearchParams(searchParams.toString());
      for (const [key, value] of Object.entries(boardFilterParams(next))) {
        if (value === null) params.delete(key);
        else params.set(key, value);
      }
      params.set("view", "board");
      router.push(`/applications?${params.toString()}`);
    },
    [router, searchParams],
  );

  // The search box writes to the URL a beat after typing stops, not on every key.
  useEffect(() => {
    if (searchInput === filterState.search) return;
    const timer = setTimeout(() => updateFilter({ ...filterState, search: searchInput }), 300);
    return () => clearTimeout(timer);
  }, [searchInput, filterState, updateFilter]);

  const reload = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ["board"] });
  }, [queryClient]);

  /** Other screens read the same applications: the list, the dashboard's counts and reminders. */
  const refreshElsewhere = useCallback(async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["applications"] }),
      queryClient.invalidateQueries({ queryKey: ["dashboard"] }),
    ]);
  }, [queryClient]);

  const loadMore = useCallback(
    async (column: BoardColumn) => {
      const state = columns?.find((c) => c.column === column);
      if (!state?.nextCursor || loadingColumns.includes(column)) return;
      setLoadingColumns((current) => [...current, column]);
      try {
        const page = await boardApi.getColumn(column, requestFilter.current, state.nextCursor);
        setColumns((current) => (current ? appendPage(current, page) : current));
      } catch {
        setError(t("actionFailed"));
      } finally {
        setLoadingColumns((current) => current.filter((c) => c !== column));
      }
    },
    [columns, loadingColumns, t],
  );

  const commitMove = useCallback(
    async (card: BoardCardResponse, target: BoardColumn, index: number, status: ApplicationStatus | null) => {
      if (!columns) return;
      const changesStatus = status !== null && status !== card.status;
      const next = applyMove(columns, card.id, target, index, changesStatus ? status : null, new Date(), closedVisibleDays);
      const targetCards = next.find((c) => c.column === target)?.cards ?? [];
      const { aboveCardId, belowCardId } = neighboursOf(targetCards, card.id);
      setColumns(next);
      if (card.unseen) setUnseenCount((count) => Math.max(0, count - 1));
      setError(null);
      try {
        await boardApi.move(card.id, { toStatus: changesStatus ? status : null, aboveCardId, belowCardId });
        if (changesStatus) await refreshElsewhere();
      } catch {
        setError(t("moveFailed"));
        await reload();
      }
    },
    [closedVisibleDays, columns, refreshElsewhere, reload, t],
  );

  const actions: CardActions = useMemo(
    () => ({
      onSeen: (card) => {
        if (!card.unseen) return;
        setColumns((current) =>
          current?.map((state) => ({
            ...state,
            cards: state.cards.map((c) => (c.id === card.id ? { ...c, unseen: false } : c)),
          })) ?? current,
        );
        setUnseenCount((count) => Math.max(0, count - 1));
        boardApi.markSeen({ cardIds: [card.id], all: false }).catch(() => undefined);
      },
      onChangeStage: (card) =>
        setPendingStage({
          card,
          target: null,
          index: 0,
          statuses: APPLICATION_STATUSES,
        }),
      onMoveToTop: (card) => {
        const found = columns ? findCard(columns, card.id) : null;
        if (found) void commitMove(card, found.column, 0, null);
      },
      onRemove: (card) => {
        setColumns((current) => (current ? removeCard(current, card.id) : current));
        if (card.unseen) setUnseenCount((count) => Math.max(0, count - 1));
        boardApi.remove(card.id).catch(async () => {
          setError(t("actionFailed"));
          await reload();
        });
      },
    }),
    [columns, commitMove, reload, t],
  );

  // Mouse and touch apart, not one PointerSensor: on a phone a finger that starts a scroll on a card
  // would move 6px and pick the card up. Touch starts a drag only after a press-and-hold, so a
  // swipe still scrolls the column.
  const sensors = useSensors(
    useSensor(MouseSensor, { activationConstraint: { distance: 6 } }),
    useSensor(TouchSensor, { activationConstraint: { delay: 250, tolerance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  /**
   * The column under the pointer decides where a card goes; inside it, the nearest card decides
   * the place. closestCorners alone measured every card and column on the board, so over a short or
   * empty column the corners of a card in the next column were often "closer" and the card landed
   * in the wrong column. A keyboard drag has no pointer and keeps closestCorners.
   */
  const collisionDetection: CollisionDetection = (args) => {
    const columnHit = pointerWithin(args).find((hit) => String(hit.id).startsWith(COLUMN_DROP_PREFIX));
    if (!columnHit) return closestCorners(args);
    const column = String(columnHit.id).slice(COLUMN_DROP_PREFIX.length);
    const cards = columns?.find((c) => c.column === column)?.cards ?? [];
    const cardIds = new Set(cards.map((card) => card.id));
    // Below the last card is "the end of the column", not "before the nearest card" — which is
    // what closestCorners would say about the empty space under the last one.
    const lastRect = cards.length > 0 ? args.droppableRects.get(cards[cards.length - 1].id) : undefined;
    if (args.pointerCoordinates && lastRect && args.pointerCoordinates.y > lastRect.bottom) return [columnHit];
    const nearest = closestCorners({
      ...args,
      droppableContainers: args.droppableContainers.filter((container) => cardIds.has(String(container.id))),
    });
    return nearest.length > 0 ? nearest : [columnHit];
  };

  const columnOfOver = (overId: string | number | undefined): BoardColumn | null => {
    if (overId === undefined || !columns) return null;
    const id = String(overId);
    if (id.startsWith(COLUMN_DROP_PREFIX)) return id.slice(COLUMN_DROP_PREFIX.length) as BoardColumn;
    return findCard(columns, id)?.column ?? null;
  };

  const onDragStart = (event: DragStartEvent) => {
    setActiveCard((event.active.data.current?.card as BoardCardResponse | undefined) ?? null);
  };

  const onDragOver = (event: DragOverEvent) => {
    const column = columnOfOver(event.over?.id);
    const from = activeCard && columns ? findCard(columns, activeCard.id)?.column : null;
    setOverColumn(column && column !== from ? column : null);
  };

  const onDragEnd = (event: DragEndEvent) => {
    setActiveCard(null);
    setOverColumn(null);
    if (!columns || !event.over) return;
    const found = findCard(columns, String(event.active.id));
    if (!found) return;
    const overId = String(event.over.id);

    let target: BoardColumn;
    let index: number;
    if (overId.startsWith(COLUMN_DROP_PREFIX)) {
      target = overId.slice(COLUMN_DROP_PREFIX.length) as BoardColumn;
      const cards = columns.find((c) => c.column === target)?.cards ?? [];
      index = target === found.column ? cards.length - 1 : cards.length;
    } else {
      const over = findCard(columns, overId);
      if (!over) return;
      target = over.column;
      index = over.index;
    }

    if (target === found.column) {
      if (index !== found.index) void commitMove(found.card, target, index, null);
      return;
    }
    if (!canDrop(found.card, target)) return;
    const status = statusForDrop(target);
    if (status) {
      void commitMove(found.card, target, index, status);
    } else {
      setPendingStage({ card: found.card, target, index, statuses: statusesForColumn(target) });
    }
  };

  const titleOf = (id: string | number) => {
    const card = columns ? findCard(columns, String(id))?.card : undefined;
    return card ? `${card.jobTitle}, ${card.companyName}` : "";
  };
  const columnName = (id: string | number | undefined) => {
    const column = columnOfOver(id);
    return column ? tColumns(column) : "";
  };

  const filterCount = activeFilterCount(filterState);
  const chips: { key: string; label: string; clear: Partial<BoardFilterState> }[] = [
    ...(filterState.window
      ? [{ key: "window", label: tChips("window", { days: filterState.window }), clear: { window: null } }]
      : filterState.from || filterState.to
        ? [{ key: "range", label: tChips("range"), clear: { from: "", to: "" } }]
        : []),
    ...filterState.sourceGroups.map((group) => ({
      key: `src-${group}`,
      label: tSource(group),
      clear: { sourceGroups: filterState.sourceGroups.filter((g) => g !== group) },
    })),
    ...(filterState.unseen ? [{ key: "unseen", label: tDrawer("unseen"), clear: { unseen: false } }] : []),
    ...(filterState.silent ? [{ key: "silent", label: tDrawer("silent", { days: silentDays }), clear: { silent: false } }] : []),
    ...(filterState.reminder ? [{ key: "reminder", label: tDrawer("reminder"), clear: { reminder: false } }] : []),
    ...(filterState.promise ? [{ key: "promise", label: tDrawer("promise"), clear: { promise: false } }] : []),
  ];

  const renderColumn = (state: ColumnState, bare = false) => (
    <BoardColumnView
      key={state.column}
      state={state}
      now={now}
      silentDays={silentDays}
      closedVisibleDays={closedVisibleDays}
      actions={actions}
      isLoadingMore={loadingColumns.includes(state.column)}
      onLoadMore={() => void loadMore(state.column)}
      isDropTarget={overColumn === state.column}
      bare={bare}
    />
  );

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        {toggle}
        <Button type="button" variant="outline" onClick={() => setAddOpen(true)}>
          {t("addFromList")}
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-2 rounded-xl border border-gray-200 bg-white p-3 dark:border-gray-800 dark:bg-gray-900">
        <label className="min-w-0 flex-1 sm:max-w-xs">
          <span className="sr-only">{t("searchLabel")}</span>
          <Input
            type="search"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            placeholder={t("searchPlaceholder")}
          />
        </label>
        <button
          type="button"
          onClick={() => setDrawerOpen(true)}
          aria-expanded={drawerOpen}
          className="inline-flex items-center gap-1.5 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-200 dark:hover:bg-gray-800"
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
            <path d="M4 6h16M7 12h10M10 18h4" />
          </svg>
          {t("filters")}
          {filterCount > 0 && <span className="rounded-full bg-accent px-1.5 text-[11px] font-semibold text-white">{filterCount}</span>}
        </button>
        {chips.map((chip) => (
          <button
            key={chip.key}
            type="button"
            onClick={() => updateFilter({ ...filterState, ...chip.clear })}
            aria-label={tChips("remove", { label: chip.label })}
            className="rounded-full bg-accent-wash px-2.5 py-1 text-xs text-accent-ink hover:bg-accent/20"
          >
            {chip.label} ×
          </button>
        ))}
        {unseenCount > 0 && (
          <div className="ml-auto flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
            <span className="h-2 w-2 rounded-full bg-accent" aria-hidden="true" />
            <span>{t("arrivals", { count: unseenCount })}</span>
            {!filterState.unseen && (
              <button type="button" onClick={() => updateFilter({ ...filterState, unseen: true })} className="text-accent-ink underline">
                {t("showArrivals")}
              </button>
            )}
            <button
              type="button"
              onClick={() => {
                setUnseenCount(0);
                boardApi
                  .markSeen({ cardIds: null, all: true })
                  .then(reload)
                  .catch(() => setError(t("actionFailed")));
              }}
              className="text-gray-600 underline dark:text-gray-400"
            >
              {t("markAllSeen")}
            </button>
          </div>
        )}
      </div>
      {filterCount > 0 && <p className="-mt-2 text-xs text-gray-500 dark:text-gray-400">{t("filterNote")}</p>}

      {error && (
        <p role="alert" className="rounded-md bg-crit-wash px-3 py-2 text-sm text-crit-ink">
          {error}
        </p>
      )}

      {boardQuery.isError && !columns ? (
        <div className="flex items-center gap-3 text-sm text-gray-600 dark:text-gray-400">
          {t("loadError")}
          <Button type="button" variant="secondary" onClick={() => void reload()}>
            {t("retry")}
          </Button>
        </div>
      ) : !columns ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
      ) : (
        <DndContext
          sensors={sensors}
          collisionDetection={collisionDetection}
          // Gentler than dnd-kit's default (a 20% edge zone): on a phone the column is ~330px of
          // ~160px cards, so dropping on the second card already sat in that zone and the list
          // scrolled the card past its target. A narrow, slow edge still reaches a long column's end.
          autoScroll={{ threshold: { x: 0.1, y: isDesktop ? 0.12 : 0.08 }, acceleration: isDesktop ? 8 : 4 }}
          onDragStart={onDragStart}
          onDragOver={onDragOver}
          onDragEnd={onDragEnd}
          onDragCancel={() => {
            setActiveCard(null);
            setOverColumn(null);
          }}
          accessibility={{
            screenReaderInstructions: { draggable: tDnd("instructions") },
            announcements: {
              onDragStart: ({ active }) => tDnd("pickedUp", { title: titleOf(active.id) }),
              onDragOver: ({ active, over }) => (over ? tDnd("over", { title: titleOf(active.id), column: columnName(over.id) }) : undefined),
              onDragEnd: ({ active, over }) =>
                over ? tDnd("dropped", { title: titleOf(active.id), column: columnName(over.id) }) : tDnd("cancelled"),
              onDragCancel: () => tDnd("cancelled"),
            },
          }}
        >
          {isDesktop ? (
            <div className="grid h-[calc(100dvh-17rem)] min-h-[30rem] auto-cols-[minmax(15rem,1fr)] grid-flow-col gap-3 overflow-x-auto pb-2">
              {columns.map((state) => renderColumn(state))}
            </div>
          ) : (
            <div className="flex flex-col gap-3">
              <div role="tablist" aria-label={t("stageTabs")} className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1">
                {BOARD_COLUMNS.map((column) => {
                  const state = columns.find((c) => c.column === column)!;
                  const selected = column === mobileColumn;
                  return (
                    <button
                      key={column}
                      type="button"
                      role="tab"
                      aria-selected={selected}
                      onClick={() => setMobileColumn(column)}
                      className={`min-h-11 shrink-0 rounded-full px-4 text-sm ${
                        selected
                          ? "bg-accent font-semibold text-white"
                          : "bg-white text-gray-700 ring-1 ring-inset ring-gray-300 dark:bg-gray-900 dark:text-gray-200 dark:ring-gray-700"
                      }`}
                    >
                      {tColumns(column)} · {state.total}
                    </button>
                  );
                })}
              </div>
              <div role="tabpanel" className="h-[calc(100dvh-19rem)] min-h-[26rem]">
                {renderColumn(columns.find((c) => c.column === mobileColumn)!, true)}
              </div>
            </div>
          )}
          <DragOverlay>
            {activeCard ? <BoardCardBody card={activeCard} now={now} silentDays={silentDays} actions={actions} lifted /> : null}
          </DragOverlay>
        </DndContext>
      )}

      {drawerOpen && (
        <BoardFilterDrawer
          value={filterState}
          silentDays={silentDays}
          onApply={(next) => {
            setDrawerOpen(false);
            setSearchInput(next.search);
            updateFilter(next);
          }}
          onClose={() => setDrawerOpen(false)}
        />
      )}

      {pendingStage && (
        <StagePicker
          title={`${pendingStage.card.jobTitle} · ${pendingStage.card.companyName}`}
          statuses={pendingStage.statuses}
          current={pendingStage.card.status}
          onClose={() => setPendingStage(null)}
          onPick={(status) => {
            const { card, target, index } = pendingStage;
            setPendingStage(null);
            void commitMove(card, target ?? columnFor(status), target ? index : 0, status);
          }}
        />
      )}

      {addOpen && (
        <AddToBoardDialog
          isSubmitting={adding}
          error={addError}
          onClose={() => {
            setAddOpen(false);
            setAddError(null);
          }}
          onAdd={(ids) => {
            setAdding(true);
            setAddError(null);
            boardApi
              .add(ids)
              .then(async () => {
                setAddOpen(false);
                await Promise.all([reload(), refreshElsewhere()]);
              })
              .catch(() => setAddError(t("actionFailed")))
              .finally(() => setAdding(false));
          }}
        />
      )}
    </div>
  );
}
