import { describe, expect, it } from "vitest";
import {
  areAllExpanded,
  COLLAPSE_GROUPS_LARGER_THAN,
  groupPageKey,
  initiallyCollapsed,
  parseCompanySortBy,
  parseFlatSortBy,
  parsePage,
  parseSortDirection,
  parseStatus,
  parseView,
  toggleAllCollapsed,
} from "./listView";

describe("parseView", () => {
  it("defaults to the flat list", () => {
    // Today's /applications link has no view parameter and has to keep opening today's list.
    expect(parseView(null)).toBe("flat");
    expect(parseView("")).toBe("flat");
    expect(parseView("kanban")).toBe("flat");
  });

  it("reads the company view back out of the URL", () => {
    expect(parseView("company")).toBe("company");
  });
});

describe("sort parsing", () => {
  it("keeps a value the view actually offers", () => {
    expect(parseFlatSortBy("JobTitle")).toBe("JobTitle");
    expect(parseCompanySortBy("ApplicationCount")).toBe("ApplicationCount");
  });

  it("falls back rather than passing on the other view's sort", () => {
    // The two views share the `sortBy` parameter, so switching with a stale value in the URL is the
    // normal case, not an edge one — and the grouped endpoint rejects a value from the flat list's
    // vocabulary. Pressing a toggle must never produce a validation error.
    expect(parseCompanySortBy("AppliedAt")).toBe("LastActivity");
    expect(parseFlatSortBy("LastActivity")).toBe("AppliedAt");
  });

  it("falls back for anything unrecognised, including a hand-edited URL", () => {
    expect(parseFlatSortBy(null)).toBe("AppliedAt");
    expect(parseFlatSortBy("; drop table")).toBe("AppliedAt");
    expect(parseCompanySortBy(null)).toBe("LastActivity");
    expect(parseCompanySortBy("companyname")).toBe("LastActivity");
  });

  it("defaults the direction to descending", () => {
    expect(parseSortDirection(null)).toBe("Descending");
    expect(parseSortDirection("Ascending")).toBe("Ascending");
    expect(parseSortDirection("ascending")).toBe("Descending");
  });
});

describe("parseStatus", () => {
  it("keeps a real status and drops anything else", () => {
    expect(parseStatus("Rejected")).toBe("Rejected");
    expect(parseStatus(null)).toBe("");
    expect(parseStatus("Rejcted")).toBe("");
  });
});

describe("parsePage", () => {
  it("keeps a whole page number from 1 up", () => {
    expect(parsePage("3")).toBe(3);
  });

  it("falls back to the first page for anything that is not one", () => {
    expect(parsePage(null)).toBe(1);
    expect(parsePage("0")).toBe(1);
    expect(parsePage("-2")).toBe(1);
    expect(parsePage("1.5")).toBe(1);
    expect(parsePage("last")).toBe(1);
  });
});

describe("initiallyCollapsed", () => {
  const group = (companyId: string, rows: number) => ({
    companyId,
    applications: Array.from({ length: rows }, (_, i) => ({ id: `${companyId}-${i}` })),
  });

  it("leaves ordinary groups open", () => {
    // The reason to be on this screen is to see the applications; folding a company with two of
    // them hides everything and shows nothing in return.
    expect(initiallyCollapsed([group("getir", 1), group("trendyol", COLLAPSE_GROUPS_LARGER_THAN)]))
      .toEqual([]);
  });

  it("folds a group big enough to fill the page on its own", () => {
    expect(initiallyCollapsed([group("getir", 2), group("kalabalik", 20)])).toEqual(["kalabalik"]);
  });

  it("folds nothing on an empty page", () => {
    expect(initiallyCollapsed([])).toEqual([]);
  });
});

describe("groupPageKey", () => {
  it("changes when the companies on screen change", () => {
    // What the key is for: the fold state has to go back to its defaults when the page moves, or a
    // company id left over from the previous page folds the wrong row.
    expect(groupPageKey([{ companyId: "a" }, { companyId: "b" }]))
      .not.toBe(groupPageKey([{ companyId: "a" }, { companyId: "c" }]));
  });

  it("stays the same for the same companies in the same order", () => {
    expect(groupPageKey([{ companyId: "a" }, { companyId: "b" }]))
      .toBe(groupPageKey([{ companyId: "a" }, { companyId: "b" }]));
  });

  it("changes when the order changes, since the rows are drawn in it", () => {
    expect(groupPageKey([{ companyId: "a" }, { companyId: "b" }]))
      .not.toBe(groupPageKey([{ companyId: "b" }, { companyId: "a" }]));
  });
});

describe("toggleAllCollapsed", () => {
  const page = [{ companyId: "getir" }, { companyId: "trendyol" }, { companyId: "kalabalik" }];

  it("opens every company when one of them is folded", () => {
    // The one control does whichever of the two is still left to do, so a page that arrived with a
    // big group folded (the common case) opens on the first click rather than closing the rest.
    expect(toggleAllCollapsed(page, ["kalabalik"])).toEqual([]);
  });

  it("closes every company only from fully open", () => {
    expect(toggleAllCollapsed(page, [])).toEqual(["getir", "trendyol", "kalabalik"]);
  });

  it("folds only the companies on this page", () => {
    // A carried-over id would fold a row that is not on screen — and reappear as a folded company
    // if the user paged back.
    expect(toggleAllCollapsed([{ companyId: "getir" }], [])).toEqual(["getir"]);
  });

  it("has nothing to fold on an empty page", () => {
    expect(toggleAllCollapsed([], [])).toEqual([]);
  });
});

describe("areAllExpanded", () => {
  it("is true only when no company is folded", () => {
    expect(areAllExpanded([])).toBe(true);
    expect(areAllExpanded(["kalabalik"])).toBe(false);
  });
});
