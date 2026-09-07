import { describe, expect, it } from "vitest";
import type { ApplicationStatus, ApplicationSummaryResponse } from "@/types/api";
import {
  EMPTY_SELECTION,
  type SelectionState,
  canOfferAllMatching,
  countAlreadyInStatus,
  defaultTargetStatus,
  expectedCountFor,
  isPagePartiallySelected,
  isRowSelected,
  isSelectionEmpty,
  isWholePageSelected,
  selectAllMatching,
  selectedItems,
  selectionCount,
  toBulkSelection,
  toUndoEntries,
  toggleAllOnPage,
  toggleRow,
} from "./bulkSelection";

const PAGE = ["a", "b", "c"];

function pageSelection(...ids: string[]): SelectionState {
  return { kind: "page", ids };
}

function item(id: string, status: ApplicationStatus): ApplicationSummaryResponse {
  return {
    id,
    companyName: `Company ${id}`,
    jobTitle: `Role ${id}`,
    status,
    appliedAt: "2026-09-01T00:00:00Z",
    updatedAt: "2026-09-01T00:00:00Z",
  };
}

describe("counting", () => {
  it("counts an explicit selection by its ids", () => {
    expect(selectionCount(pageSelection("a", "b"))).toBe(2);
    expect(isSelectionEmpty(pageSelection("a"))).toBe(false);
  });

  it("counts an all-matching selection by the number that was on screen", () => {
    expect(selectionCount(selectAllMatching(247))).toBe(247);
  });

  it("starts empty", () => {
    expect(isSelectionEmpty(EMPTY_SELECTION)).toBe(true);
  });
});

describe("toggleRow", () => {
  it("adds and removes an id", () => {
    expect(toggleRow(EMPTY_SELECTION, "a", PAGE)).toEqual(pageSelection("a"));
    expect(toggleRow(pageSelection("a", "b"), "a", PAGE)).toEqual(pageSelection("b"));
  });

  it("drops out of all-matching to this page minus the unticked row", () => {
    // Otherwise one click would quietly turn "all 247" into "247 minus one", which is not what
    // unticking a single row looks like it does.
    expect(toggleRow(selectAllMatching(247), "b", PAGE)).toEqual(pageSelection("a", "c"));
  });
});

describe("toggleAllOnPage", () => {
  it("selects the whole page when it is not fully selected", () => {
    expect(toggleAllOnPage(pageSelection("a"), PAGE)).toEqual(pageSelection("a", "b", "c"));
  });

  it("clears when the page is already fully selected", () => {
    expect(toggleAllOnPage(pageSelection("a", "b", "c"), PAGE)).toEqual(EMPTY_SELECTION);
  });

  it("clears an all-matching selection rather than re-selecting the page", () => {
    expect(toggleAllOnPage(selectAllMatching(247), PAGE)).toEqual(EMPTY_SELECTION);
  });
});

describe("header checkbox state", () => {
  it("is unchecked and not indeterminate on an empty page", () => {
    expect(isWholePageSelected(EMPTY_SELECTION, [])).toBe(false);
    expect(isPagePartiallySelected(EMPTY_SELECTION, [])).toBe(false);
  });

  it("is indeterminate for a partial selection", () => {
    expect(isPagePartiallySelected(pageSelection("a"), PAGE)).toBe(true);
    expect(isWholePageSelected(pageSelection("a"), PAGE)).toBe(false);
  });

  it("is checked once every row on the page is selected", () => {
    expect(isWholePageSelected(pageSelection("a", "b", "c"), PAGE)).toBe(true);
    expect(isPagePartiallySelected(pageSelection("a", "b", "c"), PAGE)).toBe(false);
  });
});

describe("isRowSelected", () => {
  it("covers every visible row in all-matching mode", () => {
    expect(isRowSelected(selectAllMatching(247), "z")).toBe(true);
  });

  it("checks membership otherwise", () => {
    expect(isRowSelected(pageSelection("a"), "a")).toBe(true);
    expect(isRowSelected(pageSelection("a"), "b")).toBe(false);
  });
});

describe("canOfferAllMatching", () => {
  it("offers only once the whole page is selected and more rows exist behind it", () => {
    expect(canOfferAllMatching(pageSelection("a", "b", "c"), PAGE, 247)).toBe(true);
  });

  it("does not offer while the user is still picking rows", () => {
    expect(canOfferAllMatching(pageSelection("a"), PAGE, 247)).toBe(false);
  });

  it("does not offer when the page is the whole result set", () => {
    expect(canOfferAllMatching(pageSelection("a", "b", "c"), PAGE, 3)).toBe(false);
  });

  it("does not offer again once all-matching is already on", () => {
    expect(canOfferAllMatching(selectAllMatching(247), PAGE, 247)).toBe(false);
  });
});

describe("toBulkSelection", () => {
  it("sends the ids for an explicit selection", () => {
    expect(toBulkSelection(pageSelection("a", "b"), { search: "", status: "" })).toEqual({ ids: ["a", "b"] });
  });

  it("sends the list's live filter for an all-matching selection", () => {
    expect(toBulkSelection(selectAllMatching(9), { search: "acme", status: "Rejected" })).toEqual({
      allMatching: { search: "acme", status: "Rejected" },
    });
  });

  it("sends nulls rather than empty strings for an unset filter", () => {
    // "" would be a search for the empty string on the wire; the server treats null as "no filter".
    expect(toBulkSelection(selectAllMatching(9), { search: "", status: "" })).toEqual({
      allMatching: { search: null, status: null },
    });
  });
});

describe("expectedCountFor", () => {
  it("guards an all-matching selection with the count that was shown", () => {
    expect(expectedCountFor(selectAllMatching(247))).toBe(247);
  });

  it("sends no count for an explicit id list", () => {
    expect(expectedCountFor(pageSelection("a"))).toBeNull();
  });
});

describe("countAlreadyInStatus", () => {
  const items = [item("a", "Rejected"), item("b", "Applied"), item("c", "Rejected")];

  it("counts only the selected rows already in the target status", () => {
    expect(countAlreadyInStatus(pageSelection("a", "b"), items, "Rejected")).toBe(1);
  });

  it("returns null for an all-matching selection rather than guessing from one page", () => {
    expect(countAlreadyInStatus(selectAllMatching(247), items, "Rejected")).toBeNull();
  });
});

describe("selectedItems", () => {
  const items = [item("a", "Applied"), item("b", "Applied")];

  it("returns the rows behind an explicit selection", () => {
    expect(selectedItems(pageSelection("b"), items).map((i) => i.id)).toEqual(["b"]);
  });

  it("returns nothing for an all-matching selection, which has no listable set", () => {
    expect(selectedItems(selectAllMatching(247), items)).toEqual([]);
  });
});

describe("toUndoEntries", () => {
  it("reverses each change, expecting the status the server just wrote", () => {
    expect(
      toUndoEntries([
        { applicationId: "a", fromStatus: "Applied", toStatus: "Rejected" },
        { applicationId: "b", fromStatus: "Interview", toStatus: "Rejected" },
      ]),
    ).toEqual([
      { applicationId: "a", expectedStatus: "Rejected", revertTo: "Applied" },
      { applicationId: "b", expectedStatus: "Rejected", revertTo: "Interview" },
    ]);
  });

  it("produces nothing when nothing moved", () => {
    expect(toUndoEntries([])).toEqual([]);
  });
});

describe("defaultTargetStatus", () => {
  const STATUSES: ApplicationStatus[] = ["Applied", "Screening", "Interview"];

  it("skips past the status every selected row already shares", () => {
    // Otherwise the dialog opens saying "0 will change" with a dead confirm button, and the user
    // has to work out that the dialog is wrong rather than their selection.
    const items = [item("a", "Applied"), item("b", "Applied")];
    expect(defaultTargetStatus(pageSelection("a", "b"), items, STATUSES)).toBe("Screening");
  });

  it("falls back to the first status for a mixed selection", () => {
    const items = [item("a", "Applied"), item("b", "Interview")];
    expect(defaultTargetStatus(pageSelection("a", "b"), items, STATUSES)).toBe("Applied");
  });

  it("falls back to the first status for an all-matching selection, whose rows are unknowable", () => {
    expect(defaultTargetStatus(selectAllMatching(247), [item("a", "Applied")], STATUSES)).toBe("Applied");
  });
});
