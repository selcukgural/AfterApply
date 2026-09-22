"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { type BulkResult, BulkResultBanner } from "@/components/applications/BulkResultBanner";
import { Pagination } from "@/components/applications/Pagination";
import { SelectionCheckbox } from "@/components/applications/SelectionCheckbox";
import {
  useBulkDismissReminders,
  useBulkFollowUpReminders,
  useBulkGhostReminders,
  useDismissReminder,
  useFollowUpReminder,
  useMarkReminderGhosted,
  useReminders,
  useUndoBulkGhostReminders,
} from "@/hooks/useReminders";
import { useStaleSummary } from "@/hooks/useStaleApplications";
import { REMINDER_PAGE_SIZE } from "@/lib/api/reminders";
import { ApiError } from "@/lib/api/httpClient";
import {
  EMPTY_SELECTION,
  canOfferAllMatching,
  expectedCountFor,
  isPagePartiallySelected,
  isRowSelected,
  isSelectionEmpty,
  isWholePageSelected,
  selectAllMatching,
  selectionCount,
  toggleAllOnPage,
  toggleRow,
  toUndoEntries,
  type SelectionState,
} from "@/lib/applications/bulkSelection";
import { REMINDER_ANSWER_KEY, REMINDER_LABEL_KEY, answersByGhosting, clampPage, toReminderSelection } from "@/lib/dashboard/reminders";
import { formatPromiseDate } from "@/lib/applications/replyPromise";
import { formatCount } from "@/lib/dashboard/format";
import type { BulkReminderRequest, ReminderResponse } from "@/types/api";

/**
 * The reminders the API has always generated (a follow-up is due, or an application has gone quiet
 * long enough to count as ghosted), finally shown somewhere. Renders nothing while loading, on
 * error and when there is nothing to nudge about: the board above is the point of the page, and an
 * empty "reminders" card would be a fourth tile saying nothing.
 *
 * Five rows a page, since 2026-09-13's second look at a real account: the horizon keeps the scan
 * from creating rows about a 2017 import, but nothing kept the card from drawing the 1,224 that
 * already existed, and even inside the horizon a busy season is more than one screen. So the list
 * pages, and answers come in two sizes — the row's own buttons, and a selection: the page's five
 * through the header checkbox, or every reminder through the "select all" link that follows it,
 * the same page/all split the applications list makes. The selection turns the card's header into
 * the action bar (design canvas, variant B) rather than floating one over the dashboard.
 *
 * "Mark as ghosted" in bulk is a status change on the applications behind the selection, so it
 * reports through the same result strip as the applications page, undo included. Dismiss and
 * "followed up" only close rows; the list shrinking is their whole report.
 */
export function RemindersPanel() {
  const t = useTranslations("dashboard.reminders");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const [page, setPage] = useState(1);
  const [selection, setSelection] = useState<SelectionState>(EMPTY_SELECTION);
  const [result, setResult] = useState<BulkResult | null>(null);
  const [bulkError, setBulkError] = useState<string | null>(null);
  const [undoError, setUndoError] = useState<string | null>(null);
  const { data } = useReminders(page);
  const { data: stale } = useStaleSummary();
  const dismiss = useDismissReminder();
  const followUp = useFollowUpReminder();
  const markGhosted = useMarkReminderGhosted();
  const bulkDismiss = useBulkDismissReminders();
  const bulkFollowUp = useBulkFollowUpReminders();
  const bulkGhost = useBulkGhostReminders();
  const undoGhost = useUndoBulkGhostReminders();

  const totalCount = data?.totalCount ?? 0;

  // Answering the last row of the last page leaves the page empty: land on the new last page.
  // Adjusted during render rather than in an effect (react.dev, "adjusting state when a prop
  // changes"): React re-runs the component before committing, so the empty page never paints.
  if (data && page > clampPage(page, data.totalCount, REMINDER_PAGE_SIZE)) {
    setPage(clampPage(page, data.totalCount, REMINDER_PAGE_SIZE));
  }

  // A selection is rows the user can see: leaving the page drops it (bulkSelection.ts).
  const changePage = (next: number) => {
    setPage(next);
    setSelection(EMPTY_SELECTION);
  };

  if (!data || (data.totalCount === 0 && result === null)) return null;

  const reminders = data.items;
  const pageIds = reminders.map((reminder) => reminder.id);
  const bulkPending = bulkDismiss.isPending || bulkFollowUp.isPending || bulkGhost.isPending;
  const isBusy = (reminder: ReminderResponse) =>
    bulkPending ||
    (dismiss.isPending && dismiss.variables === reminder.id) ||
    (followUp.isPending && followUp.variables === reminder.id) ||
    (markGhosted.isPending && markGhosted.variables?.id === reminder.id);
  const answer = (reminder: ReminderResponse) =>
    answersByGhosting(reminder.type) ? markGhosted.mutate(reminder) : followUp.mutate(reminder.id);
  const rowError = dismiss.isError || followUp.isError || markGhosted.isError;
  const showStaleNote = stale !== undefined && stale.count > 0 && !stale.suggest;
  const hasSelection = !isSelectionEmpty(selection);
  const count = selectionCount(selection);

  /** A 409 means the list moved under the user and nothing was changed — say which way it moved
   *  and let them look again rather than re-submitting for them. */
  const describeError = (error: unknown): string => {
    if (error instanceof ApiError && error.status === 409) {
      const body = error.body as { actualCount?: number } | undefined;
      return typeof body?.actualCount === "number" ? t("countMismatch", { count: formatCount(body.actualCount, locale) }) : error.message;
    }
    return error instanceof ApiError ? error.message : tErrors("generic");
  };

  const request: BulkReminderRequest = {
    selection: toReminderSelection(selection),
    expectedCount: expectedCountFor(selection),
  };
  const bulkOptions = {
    onSuccess: () => {
      setSelection(EMPTY_SELECTION);
      setBulkError(null);
    },
    onError: (error: unknown) => setBulkError(describeError(error)),
  };
  const ghostSelection = () =>
    bulkGhost.mutate(request, {
      ...bulkOptions,
      onSuccess: (response) => {
        bulkOptions.onSuccess();
        setUndoError(null);
        setResult({
          kind: "statusChanged",
          updated: response.updated,
          skipped: response.skippedAlreadyInStatus,
          changes: response.changes,
        });
      },
    });

  return (
    <Card>
      {/* The header doubles as the action bar: with nothing selected it names the card and counts
          the list; with a selection it says what is selected and offers the three answers. */}
      <div className="mb-3 flex flex-wrap items-center justify-between gap-x-3 gap-y-2">
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
          {reminders.length > 0 ? (
            <SelectionCheckbox
              checked={isWholePageSelected(selection, pageIds)}
              indeterminate={isPagePartiallySelected(selection, pageIds)}
              onChange={() => setSelection(toggleAllOnPage(selection, pageIds))}
              label={t("selectPage")}
            />
          ) : null}
          <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</h3>
          {selection.kind === "allMatching" ? (
            <span className="text-xs text-warn-ink">
              {t("allSelected", { count: formatCount(selection.countWhenSelected, locale) })}{" "}
              <button
                type="button"
                onClick={() => setSelection(EMPTY_SELECTION)}
                className="font-medium underline underline-offset-2"
              >
                {t("clearSelection")}
              </button>
            </span>
          ) : hasSelection ? (
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {t("selectedCount", { count })}{" "}
              {canOfferAllMatching(selection, pageIds, totalCount) ? (
                <button
                  type="button"
                  onClick={() => setSelection(selectAllMatching(totalCount))}
                  className="font-medium text-accent-ink underline underline-offset-2"
                >
                  {t("selectAll", { count: formatCount(totalCount, locale) })}
                </button>
              ) : (
                <button
                  type="button"
                  onClick={() => setSelection(EMPTY_SELECTION)}
                  className="font-medium text-accent-ink underline underline-offset-2"
                >
                  {t("clearSelection")}
                </button>
              )}
            </span>
          ) : null}
        </div>
        {hasSelection ? (
          <div className="flex items-center gap-2">
            <Button type="button" variant="outline" className="px-3 py-1 text-xs" disabled={bulkPending} onClick={ghostSelection}>
              {t("markGhosted")}
            </Button>
            <Button
              type="button"
              variant="outline"
              className="px-3 py-1 text-xs"
              disabled={bulkPending}
              onClick={() => bulkFollowUp.mutate(request, bulkOptions)}
            >
              {t("followedUp")}
            </Button>
            <Button
              type="button"
              variant="secondary"
              className="px-3 py-1 text-xs"
              disabled={bulkPending}
              onClick={() => bulkDismiss.mutate(request, bulkOptions)}
            >
              {t("dismiss")}
            </Button>
          </div>
        ) : (
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("count", { count: formatCount(totalCount, locale) })}</span>
        )}
      </div>
      {bulkError ? <p className="mb-3 text-xs text-red-600 dark:text-red-400">{bulkError}</p> : null}
      {result ? (
        // The result strip outlives the selection that produced it: the undo has to stay until it
        // is closed, even once every row on the page has moved.
        <div className="mb-3">
          <BulkResultBanner
            result={result}
            isUndoing={undoGhost.isPending}
            undoError={undoError}
            // A batch just closed as ghosted is the moment the experience form has a reason to
            // exist for this person: what those processes were like is exactly what the next
            // candidate cannot find out anywhere else. Company left unselected — a batch spans
            // several — and the sentence stays a sentence, not a call to action.
            aside={
              result.kind === "statusChanged" && result.updated > 0 ? (
                <span className="text-sm text-gray-500 dark:text-gray-400">
                  {t("shareAfterGhost")}{" "}
                  <Link href="/contribute?tab=experience" className="font-medium text-gray-700 hover:underline dark:text-gray-300">
                    {t("shareAfterGhostLink")}
                  </Link>
                </span>
              ) : null
            }
            onUndo={() => {
              if (result.kind !== "statusChanged") return;
              undoGhost.mutate(toUndoEntries(result.changes), {
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
        </div>
      ) : null}
      <ul className="flex flex-col divide-y divide-gray-100 dark:divide-gray-800">
        {reminders.map((reminder) => (
          <li key={reminder.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-2.5 first:pt-0 last:pb-0">
            <div className="flex min-w-0 items-center gap-3">
              <SelectionCheckbox
                checked={isRowSelected(selection, reminder.id)}
                indeterminate={false}
                onChange={() => setSelection(toggleRow(selection, reminder.id, pageIds))}
                label={t("selectRow", { company: reminder.companyName, title: reminder.jobTitle })}
              />
              <div className="flex min-w-0 flex-col gap-0.5">
                <Link
                  href={`/applications/${reminder.applicationId}`}
                  className="truncate text-sm font-medium text-gray-900 hover:underline dark:text-gray-100"
                >
                  {reminder.companyName} — {reminder.jobTitle}
                </Link>
                <p className="text-xs text-gray-500 dark:text-gray-400">
                  {t(REMINDER_LABEL_KEY[reminder.type])} ·{" "}
                  {/* A missed promise is read against the date they gave, not against a count of
                      silent days — that date is the whole reason to write to them now. */}
                  {reminder.type === "PromiseMissed" && reminder.promisedReplyBy
                    ? t("promiseMissedSince", {
                        date: formatPromiseDate(reminder.promisedReplyBy, locale),
                        count: formatCount(reminder.daysElapsed, locale),
                      })
                    : t("days", { count: formatCount(reminder.daysElapsed, locale) })}
                  {/* The user's own norm next to the silence, so "31 days" is read against "usually
                      9" rather than against nothing — the permission to stop waiting comes from
                      their own history, not from a threshold in a config file. Only for the ghost
                      row: a follow-up row is about an application that already replied. */}
                  {reminder.type === "PossiblyGhosted" && reminder.userMedianResponseDays != null ? (
                    <>
                      {" — "}
                      <span className="font-medium text-gray-700 dark:text-gray-300">
                        {t("usualReply", { median: formatCount(reminder.userMedianResponseDays, locale) })}
                      </span>
                    </>
                  ) : null}
                </p>
              </div>
            </div>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                className="px-3 py-1 text-xs"
                disabled={isBusy(reminder)}
                onClick={() => answer(reminder)}
              >
                {t(REMINDER_ANSWER_KEY[reminder.type])}
              </Button>
              <Button
                type="button"
                variant="secondary"
                className="px-3 py-1 text-xs"
                disabled={isBusy(reminder)}
                onClick={() => dismiss.mutate(reminder.id)}
              >
                {t("dismiss")}
              </Button>
            </div>
          </li>
        ))}
      </ul>
      {rowError ? <p className="mt-2 text-xs text-red-600 dark:text-red-400">{t("error")}</p> : null}
      {totalCount > REMINDER_PAGE_SIZE ? (
        <div className="mt-3 border-t border-gray-100 pt-3 dark:border-gray-800">
          <Pagination page={page} pageSize={REMINDER_PAGE_SIZE} totalCount={totalCount} unit="reminders" onPageChange={changePage} />
        </div>
      ) : null}
      {showStaleNote ? (
        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-3 gap-y-1 border-t border-gray-100 pt-3 dark:border-gray-800">
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {t("staleNote", { count: stale.count, threshold: stale.thresholdDays })}
          </span>
          <Link href="/applications?status=Applied" className="text-xs font-medium text-accent-ink hover:underline">
            {t("staleBulkLink")}
          </Link>
        </div>
      ) : null}
    </Card>
  );
}
