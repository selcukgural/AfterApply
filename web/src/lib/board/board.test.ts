import { describe, expect, it } from "vitest";
import type { BoardCardResponse, BoardColumnPage } from "@/types/api";
import {
  activeFilterCount,
  appendPage,
  applyMove,
  boardFilterParams,
  canDrop,
  cardAge,
  columnFor,
  companyInitials,
  companyTint,
  EMPTY_BOARD_FILTER,
  isArrival,
  neighboursOf,
  parseBoardFilter,
  removeCard,
  sourceGroupOf,
  statusForDrop,
  statusesForColumn,
  toBoardFilter,
  toColumnStates,
} from "./board";

const NOW = new Date("2026-09-27T12:00:00Z");

function card(id: string, overrides: Partial<BoardCardResponse> = {}): BoardCardResponse {
  return {
    id,
    kind: "Application",
    itemId: `item-${id}`,
    jobTitle: "Backend Engineer",
    companyId: "c1",
    companyName: "Akbank",
    status: "Applied",
    source: "LinkedIn",
    lastActivityAt: NOW.toISOString(),
    origin: "Seed",
    unseen: false,
    leavesBoardAt: null,
    ...overrides,
  };
}

function page(column: BoardColumnPage["column"], cards: BoardCardResponse[], total = cards.length, nextCursor: string | null = null): BoardColumnPage {
  return { column, cards, total, nextCursor };
}

const daysAgo = (days: number) => new Date(NOW.getTime() - days * 86_400_000).toISOString();

describe("columns", () => {
  it("maps every status to the column the server puts it in", () => {
    expect(columnFor("Applied")).toBe("Applied");
    expect(columnFor("Screening")).toBe("InProgress");
    expect(columnFor("FinalInterview")).toBe("InProgress");
    expect(columnFor("Offer")).toBe("Offer");
    expect(columnFor("Rejected")).toBe("Closed");
    expect(columnFor("Accepted")).toBe("Closed");
  });

  it("asks for a stage only where a column holds several", () => {
    expect(statusForDrop("Applied")).toBe("Applied");
    expect(statusForDrop("Offer")).toBe("Offer");
    expect(statusForDrop("InProgress")).toBeNull();
    expect(statusForDrop("Closed")).toBeNull();
    expect(statusesForColumn("InProgress")).toHaveLength(4);
    expect(statusesForColumn("Closed")).toContain("Ghosted");
  });

  it("never lets an application go back to being a saved posting", () => {
    expect(canDrop(card("a"), "Saved")).toBe(false);
    expect(canDrop(card("s", { kind: "SavedPosting", status: null }), "InProgress")).toBe(true);
  });
});

describe("cardAge", () => {
  it("measures silence from the last activity", () => {
    expect(cardAge(card("a", { lastActivityAt: daysAgo(2) }), NOW, 14)).toEqual({ age: { kind: "moved", days: 2 }, tone: "fresh" });
    expect(cardAge(card("a", { lastActivityAt: daysAgo(9) }), NOW, 14).tone).toBe("quiet");
    expect(cardAge(card("a", { lastActivityAt: daysAgo(14) }), NOW, 14).tone).toBe("silent");
    expect(cardAge(card("a", { lastActivityAt: daysAgo(31) }), NOW, 14)).toEqual({ age: { kind: "silent", days: 31 }, tone: "stale" });
  });

  it("counts down for a closed card and up for a saved posting", () => {
    const leaves = new Date(NOW.getTime() + 3.2 * 86_400_000).toISOString();
    expect(cardAge(card("a", { status: "Rejected", leavesBoardAt: leaves }), NOW, 14).age).toEqual({ kind: "leaves", days: 3 });
    // Closed a few seconds after the board loaded: still "14 days", not 15.
    const justClosed = new Date(NOW.getTime() + 14 * 86_400_000 + 5_000).toISOString();
    expect(cardAge(card("a", { status: "Rejected", leavesBoardAt: justClosed }), NOW, 14).age).toEqual({ kind: "leaves", days: 14 });
    expect(cardAge(card("s", { kind: "SavedPosting", status: null, lastActivityAt: daysAgo(5) }), NOW, 14).age).toEqual({
      kind: "saved",
      days: 5,
    });
  });

  it("marks only what arrived without the user", () => {
    expect(isArrival("Seed")).toBe(false);
    expect(isArrival("Manual")).toBe(false);
    expect(isArrival("EmailReturned")).toBe(true);
    expect(isArrival("Later")).toBe(true);
  });
});

describe("sources", () => {
  it("groups a posting's site the way the filter offers it", () => {
    expect(sourceGroupOf("LinkedIn")).toBe("linkedIn");
    expect(sourceGroupOf("KariyerNet")).toBe("kariyerNet");
    expect(sourceGroupOf("Greenhouse")).toBe("otherSites");
    expect(sourceGroupOf("Manual")).toBe("manual");
    expect(sourceGroupOf(null)).toBeNull();
  });
});

describe("filter in the URL", () => {
  it("round-trips through the query string", () => {
    const state = {
      ...EMPTY_BOARD_FILTER,
      search: "back",
      sourceGroups: ["kariyerNet" as const, "linkedIn" as const],
      window: 30 as const,
      silent: true,
      unseen: true,
    };
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(boardFilterParams(state))) {
      if (value !== null) params.set(key, value);
    }

    // Groups come back in display order, whatever order they were ticked in.
    expect(parseBoardFilter(params)).toEqual({ ...state, sourceGroups: ["linkedIn", "kariyerNet"] });
  });

  it("ignores values it does not know", () => {
    const parsed = parseBoardFilter(new URLSearchParams("active=45&src=myspace,linkedIn&from=yesterday&silent=yes"));
    expect(parsed.window).toBeNull();
    expect(parsed.sourceGroups).toEqual(["linkedIn"]);
    expect(parsed.from).toBe("");
    expect(parsed.silent).toBe(false);
  });

  it("turns a window into a from date and a custom range into both ends", () => {
    const windowed = toBoardFilter({ ...EMPTY_BOARD_FILTER, window: 7 }, NOW);
    expect(windowed.activeFrom).toBe(daysAgo(7));
    expect(windowed.activeTo).toBeUndefined();

    const ranged = toBoardFilter({ ...EMPTY_BOARD_FILTER, from: "2026-09-01", to: "2026-09-10" }, NOW);
    expect(ranged.activeFrom).toBeDefined();
    expect(new Date(ranged.activeTo!).getTime()).toBeGreaterThan(new Date(ranged.activeFrom!).getTime());
  });

  it("expands source groups into the sources the API filters on", () => {
    expect(toBoardFilter({ ...EMPTY_BOARD_FILTER, sourceGroups: ["kariyerNet"] }, NOW).sources).toEqual(["KariyerNet"]);
    expect(toBoardFilter(EMPTY_BOARD_FILTER, NOW).sources).toEqual([]);
  });

  it("counts the drawer's filters, not the search box", () => {
    expect(activeFilterCount({ ...EMPTY_BOARD_FILTER, search: "x" })).toBe(0);
    expect(activeFilterCount({ ...EMPTY_BOARD_FILTER, window: 7, silent: true, sourceGroups: ["manual"] })).toBe(3);
  });
});

describe("local moves", () => {
  const columns = () =>
    toColumnStates([
      page("Saved", [card("s1", { kind: "SavedPosting", status: null })]),
      page("Applied", [card("a1"), card("a2"), card("a3")], 25, "cursor"),
      page("InProgress", [card("p1", { status: "Interview" })]),
    ]);

  it("fills every column, empty ones included", () => {
    const states = columns();
    expect(states.map((s) => s.column)).toEqual(["Saved", "Applied", "InProgress", "Offer", "Closed"]);
    expect(states[3]).toEqual({ column: "Offer", total: 0, cards: [], nextCursor: null });
  });

  it("reorders inside a column without touching the totals", () => {
    const moved = applyMove(columns(), "a3", "Applied", 0, null);
    const applied = moved.find((s) => s.column === "Applied")!;
    expect(applied.cards.map((c) => c.id)).toEqual(["a3", "a1", "a2"]);
    expect(applied.total).toBe(25);
    expect(neighboursOf(applied.cards, "a3")).toEqual({ aboveCardId: null, belowCardId: "a1" });
  });

  it("carries the new status across columns and clears the arrival mark", () => {
    const start = columns();
    start[1].cards[1] = card("a2", { unseen: true, origin: "Email" });
    start[1].cards[1].lastActivityAt = daysAgo(20);
    const moved = applyMove(start, "a2", "InProgress", 1, "TechnicalInterview", NOW);
    const target = moved.find((s) => s.column === "InProgress")!;
    expect(target.cards.map((c) => c.id)).toEqual(["p1", "a2"]);
    expect(target.cards[1]).toMatchObject({ status: "TechnicalInterview", unseen: false, lastActivityAt: NOW.toISOString() });
    expect(target.total).toBe(2);
    expect(moved.find((s) => s.column === "Applied")!.total).toBe(24);
    expect(neighboursOf(target.cards, "a2")).toEqual({ aboveCardId: "p1", belowCardId: null });
  });

  it("starts a closed card's countdown the moment it moves", () => {
    const moved = applyMove(columns(), "a1", "Closed", 0, "Ghosted", NOW, 14);
    const card = moved.find((s) => s.column === "Closed")!.cards[0];
    expect(card.leavesBoardAt).toBe(new Date(NOW.getTime() + 14 * 86_400_000).toISOString());
    expect(cardAge(card, NOW, 14).age).toEqual({ kind: "leaves", days: 14 });
  });

  it("turns a saved posting into an application when it moves out of Saved", () => {
    const moved = applyMove(columns(), "s1", "Applied", 0, "Applied");
    expect(moved.find((s) => s.column === "Applied")!.cards[0]).toMatchObject({ id: "s1", kind: "Application", status: "Applied" });
    expect(moved.find((s) => s.column === "Saved")!.cards).toHaveLength(0);
  });

  it("appends a page without repeating a card already on screen", () => {
    const next = appendPage(columns(), page("Applied", [card("a3"), card("a4")], 25, null));
    const applied = next.find((s) => s.column === "Applied")!;
    expect(applied.cards.map((c) => c.id)).toEqual(["a1", "a2", "a3", "a4"]);
    expect(applied.nextCursor).toBeNull();
  });

  it("takes a removed card out and lowers the total", () => {
    const next = removeCard(columns(), "a1");
    expect(next.find((s) => s.column === "Applied")).toMatchObject({ total: 24 });
  });
});

describe("company mark", () => {
  it("takes the first letters of the first two words", () => {
    expect(companyInitials("Logo Yazılım")).toBe("LY");
    // Turkish casing on purpose: most names here are Turkish, and "Işık" must not become "IŞ".
    expect(companyInitials("iyzico")).toBe("İY");
    expect(companyInitials("ışık")).toBe("IŞ");
    expect(companyInitials("  Şişecam  A.Ş. ")).toBe("ŞA");
    expect(companyInitials("—")).toBe("?");
  });

  it("gives a company the same tint every time", () => {
    expect(companyTint("abc")).toBe(companyTint("abc"));
    expect(companyTint("abc")).toBeGreaterThanOrEqual(0);
    expect(companyTint("abc")).toBeLessThan(6);
  });
});
