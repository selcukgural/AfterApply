import type { SelectionState } from "@/lib/applications/bulkSelection";
import type { ReminderSelection, ReminderType } from "@/types/api";

/** Message key under `dashboard.reminders` for each reminder type. */
export const REMINDER_LABEL_KEY: Record<ReminderType, "followUp" | "possiblyGhosted" | "promiseMissed"> = {
  FollowUp: "followUp",
  PossiblyGhosted: "possiblyGhosted",
  PromiseMissed: "promiseMissed",
};

/**
 * The answer each reminder type asks for, as a message key under `dashboard.reminders`. A reminder
 * is a question — "time to follow up?", "was this ghosted?" — and until 2026-09-13 the only button
 * was "dismiss", which is not an answer to either. "Followed up" records the event; "mark ghosted"
 * changes the status. Both close the reminder.
 */
export const REMINDER_ANSWER_KEY: Record<ReminderType, "followedUp" | "markGhosted"> = {
  FollowUp: "followedUp",
  PossiblyGhosted: "markGhosted",
  // A missed date is a reason to write to them, not to close the application: the answer is the
  // follow-up, same as the plain follow-up row.
  PromiseMissed: "followedUp",
};

/** Whether a row's own answer is "mark as ghosted" (a status change) rather than "followed up". */
export function answersByGhosting(type: ReminderType): boolean {
  return REMINDER_ANSWER_KEY[type] === "markGhosted";
}

/**
 * The wire form of the card's selection. The applications list's selection state fits as it is —
 * ticked ids on the page, or "everything" with the count that was on screen — and only the
 * all-form differs: reminders have no filter to repeat back, so "all" is a flag.
 */
export function toReminderSelection(selection: SelectionState): ReminderSelection {
  return selection.kind === "allMatching" ? { all: true } : { ids: [...selection.ids] };
}

/** The last page that still has rows; 1 when there are none, so an empty list is still "page 1". */
export function lastPage(totalCount: number, pageSize: number): number {
  return Math.max(1, Math.ceil(totalCount / pageSize));
}

/**
 * Where to land after the list shrinks under the current page. Answering the only row of page 245
 * leaves page 245 empty and the total at 1,220 — the card should show page 244, not a blank list
 * with a pager that says there is nothing here.
 */
export function clampPage(page: number, totalCount: number, pageSize: number): number {
  return Math.min(page, lastPage(totalCount, pageSize));
}
