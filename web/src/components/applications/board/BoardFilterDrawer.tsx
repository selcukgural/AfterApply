"use client";

import { useEffect, useRef, useState } from "react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import {
  ACTIVITY_WINDOWS,
  type BoardFilterState,
  EMPTY_BOARD_FILTER,
  SOURCE_GROUP_ORDER,
  type SourceGroup,
} from "@/lib/board/board";
import { MAX_VIEW_NAME, readSavedViews, type SavedBoardView, withView, writeSavedViews } from "@/lib/board/savedViews";

/**
 * The board's filter sheet, sliding in from the right. Edits are drafted here and applied at once
 * with "Apply", so the board does not refetch on every tick. Built on <dialog> like Modal, for the
 * same focus-trap and Esc behaviour.
 */
export function BoardFilterDrawer({
  value,
  silentDays,
  onApply,
  onClose,
}: {
  value: BoardFilterState;
  silentDays: number;
  onApply: (next: BoardFilterState) => void;
  onClose: () => void;
}) {
  const t = useTranslations("applications.board.drawer");
  const tSource = useTranslations("applications.board.source");
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [draft, setDraft] = useState<BoardFilterState>(value);
  // Read once on open: the drawer only ever mounts in the browser, on a click.
  const [views, setViews] = useState<SavedBoardView[]>(readSavedViews);
  const [naming, setNaming] = useState(false);
  const [viewName, setViewName] = useState("");

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog || dialog.open) return;
    dialog.showModal();
    return () => dialog.close();
  }, []);

  const presets: { label: string; filter: BoardFilterState }[] = [
    { label: t("allBoard"), filter: { ...EMPTY_BOARD_FILTER, search: value.search } },
    { label: t("presetRecent"), filter: { ...EMPTY_BOARD_FILTER, search: value.search, window: 7 } },
    { label: t("presetSilent"), filter: { ...EMPTY_BOARD_FILTER, search: value.search, silent: true } },
    { label: t("presetArrivals"), filter: { ...EMPTY_BOARD_FILTER, search: value.search, unseen: true } },
    { label: t("presetPromise"), filter: { ...EMPTY_BOARD_FILTER, search: value.search, promise: true } },
  ];

  const toggleGroup = (group: SourceGroup) =>
    setDraft((current) => ({
      ...current,
      sourceGroups: current.sourceGroups.includes(group)
        ? current.sourceGroups.filter((value) => value !== group)
        : SOURCE_GROUP_ORDER.filter((value) => value === group || current.sourceGroups.includes(value)),
    }));

  const saveView = () => {
    const name = viewName.trim().slice(0, MAX_VIEW_NAME);
    if (!name) return;
    const next = withView(views, { name, filter: draft });
    setViews(next);
    writeSavedViews(next);
    setNaming(false);
    setViewName("");
  };

  const deleteView = (name: string) => {
    const next = views.filter((view) => view.name !== name);
    setViews(next);
    writeSavedViews(next);
  };

  const legend = "mb-2 text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400";
  const check = "h-4 w-4 rounded border-gray-300 text-accent focus:ring-accent";
  const row = "flex min-h-9 items-center gap-2 text-sm";
  const chip = (on: boolean) =>
    `rounded-full px-3 py-1.5 text-xs font-medium ring-1 ring-inset ${
      on ? "bg-accent-wash text-accent-ink ring-accent/60" : "text-gray-700 ring-gray-300 hover:bg-gray-50 dark:text-gray-300 dark:ring-gray-700 dark:hover:bg-gray-800"
    }`;

  return (
    <dialog
      ref={dialogRef}
      aria-label={t("title")}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target === dialogRef.current) onClose();
      }}
      className="fixed inset-y-0 left-auto right-0 m-0 h-dvh max-h-dvh w-[min(22.5rem,100vw)] max-w-none border-0 bg-white p-0 text-gray-900 shadow-2xl backdrop:bg-gray-900/30 dark:bg-gray-900 dark:text-gray-100"
    >
      <div className="flex h-full flex-col">
        <div className="flex items-center justify-between px-5 pb-2 pt-5">
          <h2 className="text-base font-semibold">{t("title")}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label={t("close")}
            className="rounded-md p-2 text-gray-500 hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
              <path d="M18 6 6 18M6 6l12 12" />
            </svg>
          </button>
        </div>

        <div className="flex flex-1 flex-col gap-6 overflow-y-auto px-5 pb-5">
          <div>
            <p className={legend}>{t("savedViews")}</p>
            <div className="flex flex-col">
              {presets.map((preset) => (
                <button
                  key={preset.label}
                  type="button"
                  onClick={() => onApply(preset.filter)}
                  className="rounded-md px-2.5 py-2 text-left text-sm hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  {preset.label}
                </button>
              ))}
              {views.map((view) => (
                <div key={view.name} className="flex items-center">
                  <button
                    type="button"
                    onClick={() => onApply(view.filter)}
                    className="flex-1 truncate rounded-md px-2.5 py-2 text-left text-sm font-medium hover:bg-gray-100 dark:hover:bg-gray-800"
                  >
                    {view.name}
                  </button>
                  <button
                    type="button"
                    onClick={() => deleteView(view.name)}
                    aria-label={t("deleteView", { name: view.name })}
                    className="rounded-md p-2 text-gray-400 hover:bg-gray-100 hover:text-gray-700 dark:hover:bg-gray-800"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" aria-hidden="true">
                      <path d="M18 6 6 18M6 6l12 12" />
                    </svg>
                  </button>
                </div>
              ))}
              {naming ? (
                <div className="mt-1 flex gap-2">
                  <label className="flex-1">
                    <span className="sr-only">{t("saveViewName")}</span>
                    <Input
                      autoFocus
                      value={viewName}
                      maxLength={MAX_VIEW_NAME}
                      onChange={(event) => setViewName(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === "Enter") saveView();
                      }}
                      placeholder={t("saveViewName")}
                    />
                  </label>
                  <Button type="button" variant="secondary" onClick={saveView}>
                    {t("saveViewConfirm")}
                  </Button>
                </div>
              ) : (
                <button
                  type="button"
                  onClick={() => setNaming(true)}
                  className="rounded-md px-2.5 py-2 text-left text-sm text-accent-ink hover:bg-accent-wash"
                >
                  {t("saveView")}
                </button>
              )}
              <p className="mt-1 px-2.5 text-xs text-gray-500 dark:text-gray-400">{t("savedViewsNote")}</p>
            </div>
          </div>

          <fieldset>
            <legend className={legend}>{t("lastActivity")}</legend>
            <div className="flex flex-wrap gap-2">
              {ACTIVITY_WINDOWS.map((days) => (
                <button
                  key={days}
                  type="button"
                  aria-pressed={draft.window === days}
                  onClick={() => setDraft((current) => ({ ...current, window: days, from: "", to: "" }))}
                  className={chip(draft.window === days)}
                >
                  {t("window", { days })}
                </button>
              ))}
              <button
                type="button"
                aria-pressed={!draft.window && !draft.from && !draft.to}
                onClick={() => setDraft((current) => ({ ...current, window: null, from: "", to: "" }))}
                className={chip(!draft.window && !draft.from && !draft.to)}
              >
                {t("anyTime")}
              </button>
            </div>
            <div className="mt-3 flex gap-2">
              <label className="flex flex-1 flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
                {t("from")}
                <Input
                  type="date"
                  // The browser draws the calendar icon; this tells it the page is dark.
                  className="dark:[color-scheme:dark]"
                  value={draft.from}
                  onChange={(event) => setDraft((current) => ({ ...current, window: null, from: event.target.value }))}
                />
              </label>
              <label className="flex flex-1 flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
                {t("to")}
                <Input
                  type="date"
                  // The browser draws the calendar icon; this tells it the page is dark.
                  className="dark:[color-scheme:dark]"
                  value={draft.to}
                  onChange={(event) => setDraft((current) => ({ ...current, window: null, to: event.target.value }))}
                />
              </label>
            </div>
          </fieldset>

          <fieldset>
            <legend className={legend}>{t("source")}</legend>
            {SOURCE_GROUP_ORDER.map((group) => (
              <label key={group} className={row}>
                <input type="checkbox" className={check} checked={draft.sourceGroups.includes(group)} onChange={() => toggleGroup(group)} />
                {tSource(group)}
              </label>
            ))}
          </fieldset>

          <fieldset>
            <legend className={legend}>{t("state")}</legend>
            <label className={row}>
              <input type="checkbox" className={check} checked={draft.unseen} onChange={(event) => setDraft((current) => ({ ...current, unseen: event.target.checked }))} />
              {t("unseen")}
            </label>
            <label className={row}>
              <input type="checkbox" className={check} checked={draft.silent} onChange={(event) => setDraft((current) => ({ ...current, silent: event.target.checked }))} />
              {t("silent", { days: silentDays })}
            </label>
            <label className={row}>
              <input type="checkbox" className={check} checked={draft.reminder} onChange={(event) => setDraft((current) => ({ ...current, reminder: event.target.checked }))} />
              {t("reminder")}
            </label>
            <label className={row}>
              <input type="checkbox" className={check} checked={draft.promise} onChange={(event) => setDraft((current) => ({ ...current, promise: event.target.checked }))} />
              {t("promise")}
            </label>
          </fieldset>
        </div>

        <div className="flex items-center justify-end gap-2 border-t border-gray-200 px-5 py-3.5 dark:border-gray-800">
          <Button type="button" variant="secondary" onClick={() => setDraft({ ...EMPTY_BOARD_FILTER, search: value.search })}>
            {t("clear")}
          </Button>
          <Button type="button" onClick={() => onApply(draft)}>
            {t("apply")}
          </Button>
        </div>
      </div>
    </dialog>
  );
}
