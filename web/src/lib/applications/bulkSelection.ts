import type {
  ApplicationStatus,
  ApplicationSummaryResponse,
  CompanyGroupResponse,
  BulkSelection,
  BulkStatusChange,
  UndoBulkStatusEntry,
} from "@/types/api";

/**
 * What the applications list has selected right now.
 *
 * Two shapes, not one, because they mean different things to the server and to the user. `page`
 * holds ids the user ticked and could count on screen. `allMatching` holds no ids at all: it is a
 * promise that the operation covers every row the current filter matches, including rows on pages
 * nobody opened — which is why it carries the count that was on screen when the user opted into it.
 *
 * Ids are deliberately not accumulated across pages: leaving the page (or changing a filter) clears
 * the selection, so "3 selected" always means three rows the user can still see. Anything wider than
 * one page goes through `allMatching`, where the count is explicit and the server checks it.
 */
export type SelectionState =
  | { readonly kind: "page"; readonly ids: readonly string[] }
  | { readonly kind: "allMatching"; readonly countWhenSelected: number };

export const EMPTY_SELECTION: SelectionState = { kind: "page", ids: [] };

export function selectionCount(selection: SelectionState): number {
  return selection.kind === "allMatching" ? selection.countWhenSelected : selection.ids.length;
}

export function isSelectionEmpty(selection: SelectionState): boolean {
  return selectionCount(selection) === 0;
}

/** In all-matching mode every visible row is covered, whether or not it was ticked individually. */
export function isRowSelected(selection: SelectionState, id: string): boolean {
  return selection.kind === "allMatching" || selection.ids.includes(id);
}

/**
 * Ticking a row while "all matching" is on would silently shrink the operation from 247 rows to
 * "this page minus one", which is not what the click looks like it does. So the first such click
 * drops back to an explicit page selection — every row on this page except the one just unticked.
 */
export function toggleRow(
  selection: SelectionState,
  id: string,
  pageIds: readonly string[],
): SelectionState {
  if (selection.kind === "allMatching") {
    return { kind: "page", ids: pageIds.filter((pageId) => pageId !== id) };
  }

  return selection.ids.includes(id)
    ? { kind: "page", ids: selection.ids.filter((selectedId) => selectedId !== id) }
    : { kind: "page", ids: [...selection.ids, id] };
}

/** The header checkbox: all of this page, or none of it. Never reaches beyond the page. */
export function toggleAllOnPage(
  selection: SelectionState,
  pageIds: readonly string[],
): SelectionState {
  return isWholePageSelected(selection, pageIds) ? EMPTY_SELECTION : { kind: "page", ids: [...pageIds] };
}

export function isWholePageSelected(selection: SelectionState, pageIds: readonly string[]): boolean {
  if (pageIds.length === 0) {
    return false;
  }
  return selection.kind === "allMatching" || pageIds.every((id) => selection.ids.includes(id));
}

export function isPagePartiallySelected(
  selection: SelectionState,
  pageIds: readonly string[],
): boolean {
  return !isSelectionEmpty(selection) && !isWholePageSelected(selection, pageIds);
}

/**
 * Whether to offer "select all N matching". Only worth showing once the whole page is ticked and
 * there is genuinely more behind it — offering it while three rows are selected would be an
 * invitation to widen a selection the user is still building.
 */
export function canOfferAllMatching(
  selection: SelectionState,
  pageIds: readonly string[],
  totalCount: number,
): boolean {
  return selection.kind === "page" && isWholePageSelected(selection, pageIds) && totalCount > pageIds.length;
}

export function selectAllMatching(totalCount: number): SelectionState {
  return { kind: "allMatching", countWhenSelected: totalCount };
}

/** Every field that decides which rows the list is showing — and nothing that decides only their
 *  order or which page they are on. */
export interface ListFilter {
  search: string;
  status: ApplicationStatus | "";
  /** Set when the list is narrowed to one company. */
  companyId: string | "";
}

/** The wire form. `allMatching` repeats the filter the list is showing, so the server resolves the
 *  same rows the user was looking at rather than a filter the client re-derived. */
export function toBulkSelection(selection: SelectionState, filter: ListFilter): BulkSelection {
  if (selection.kind === "allMatching") {
    return {
      allMatching: {
        search: filter.search === "" ? null : filter.search,
        status: filter.status === "" ? null : filter.status,
        companyId: filter.companyId === "" ? null : filter.companyId,
      },
    };
  }
  return { ids: [...selection.ids] };
}

/** Only an all-matching selection needs the count guard — an id list is already the exact set. */
export function expectedCountFor(selection: SelectionState): number | null {
  return selection.kind === "allMatching" ? selection.countWhenSelected : null;
}

/**
 * How many of the selected rows a status change would actually move. Only knowable for rows on the
 * current page, so an all-matching selection returns null and the dialog says nothing rather than
 * guessing — a wrong number in a confirmation is worse than no number.
 */
export function countAlreadyInStatus(
  selection: SelectionState,
  items: readonly ApplicationSummaryResponse[],
  newStatus: ApplicationStatus,
): number | null {
  if (selection.kind === "allMatching") {
    return null;
  }
  return items.filter((item) => selection.ids.includes(item.id) && item.status === newStatus).length;
}

/** The rows a delete confirmation lists by name. Capped by the caller; an all-matching selection
 *  has no such list, which is exactly why its confirmation shows the filter instead. */
export function selectedItems(
  selection: SelectionState,
  items: readonly ApplicationSummaryResponse[],
): ApplicationSummaryResponse[] {
  if (selection.kind === "allMatching") {
    return [];
  }
  return items.filter((item) => selection.ids.includes(item.id));
}

/** Turns what a bulk status change reported into the entries that put it back. Each carries the
 *  status the server just wrote, so anything changed since is left alone rather than overwritten. */
export function toUndoEntries(changes: readonly BulkStatusChange[]): UndoBulkStatusEntry[] {
  return changes.map((change) => ({
    applicationId: change.applicationId,
    expectedStatus: change.toStatus,
    revertTo: change.fromStatus,
  }));
}

/**
 * What the status dialog should open on.
 *
 * Opening on the first status in the list is a bad default when the selection is already in it: the
 * dialog then says "0 will change" with the confirm button dead, and the user has to work out that
 * the *dialog* is wrong rather than their selection. So when every selected row shares one status,
 * skip past it. Falls back to the first status when the selection is mixed (or unknowable, as for
 * an all-matching selection), where no single status is the wrong answer.
 */
export function defaultTargetStatus(
  selection: SelectionState,
  items: readonly ApplicationSummaryResponse[],
  statuses: readonly ApplicationStatus[],
): ApplicationStatus {
  const selected = selectedItems(selection, items);
  const shared = selected.length > 0 ? selected[0]!.status : null;
  const allShareIt = shared !== null && selected.every((item) => item.status === shared);

  return (allShareIt ? statuses.find((status) => status !== shared) : undefined) ?? statuses[0]!;
}

/**
 * The applications one company group puts on the page, in draw order.
 *
 * A group whose company holds more than the response carries (`hasMore`) contributes only the rows
 * that came back. That is the honest set: a checkbox can only mean rows the user can see and count,
 * and the group header says how many the company really holds, so the two never claim to be the
 * same number. The rest are reached through the group's own link into the flat list.
 */
export function groupIds(group: CompanyGroupResponse): string[] {
  return group.applications.map((application) => application.id);
}

/** Every visible row on a page of groups, flattened — what the page-level helpers above take. */
export function groupedPageIds(groups: readonly CompanyGroupResponse[]): string[] {
  return groups.flatMap(groupIds);
}

export function isGroupSelected(selection: SelectionState, group: CompanyGroupResponse): boolean {
  const ids = groupIds(group);
  if (ids.length === 0) {
    return false;
  }
  return selection.kind === "allMatching" || ids.every((id) => selection.ids.includes(id));
}

export function isGroupPartiallySelected(selection: SelectionState, group: CompanyGroupResponse): boolean {
  if (selection.kind === "allMatching") {
    return false;
  }
  const ids = groupIds(group);
  return ids.some((id) => selection.ids.includes(id)) && !ids.every((id) => selection.ids.includes(id));
}

/**
 * The group header's checkbox: this company's visible rows, all or none, leaving every other
 * group's selection alone. Unticking a group while "all matching" is on drops back to an explicit
 * page selection for the same reason `toggleRow` does — silently narrowing 247 rows to "this page
 * minus one company" is not what the click looks like it does.
 */
export function toggleGroup(
  selection: SelectionState,
  group: CompanyGroupResponse,
  pageIds: readonly string[],
): SelectionState {
  const ids = groupIds(group);

  if (selection.kind === "allMatching") {
    return { kind: "page", ids: pageIds.filter((pageId) => !ids.includes(pageId)) };
  }

  if (isGroupSelected(selection, group)) {
    return { kind: "page", ids: selection.ids.filter((id) => !ids.includes(id)) };
  }

  const missing = ids.filter((id) => !selection.ids.includes(id));
  return { kind: "page", ids: [...selection.ids, ...missing] };
}
