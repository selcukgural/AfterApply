import type {
  ApplicationStatus,
  BoardCardOrigin,
  BoardCardResponse,
  BoardColumn,
  BoardColumnPage,
  BoardFilter,
  Source,
} from "@/types/api";

/** Left to right, the order a job search moves in. */
export const BOARD_COLUMNS: readonly BoardColumn[] = ["Saved", "Applied", "InProgress", "Offer", "Closed"];

export const IN_PROGRESS_STATUSES: readonly ApplicationStatus[] = [
  "Screening",
  "Interview",
  "TechnicalInterview",
  "FinalInterview",
];

export const CLOSED_STATUSES: readonly ApplicationStatus[] = ["Rejected", "Ghosted", "Withdrawn", "Accepted"];

/** The same mapping the server derives a card's column with (BoardColumns.For). */
export function columnFor(status: ApplicationStatus): BoardColumn {
  if (status === "Applied") return "Applied";
  if (status === "Offer") return "Offer";
  if (IN_PROGRESS_STATUSES.includes(status)) return "InProgress";
  return "Closed";
}

/**
 * What a card dropped into `column` becomes. Null where the column holds several statuses and the
 * user has to say which (a stage picker opens), and for "Saved", which an application can never go
 * back to.
 */
export function statusForDrop(column: BoardColumn): ApplicationStatus | null {
  if (column === "Applied") return "Applied";
  if (column === "Offer") return "Offer";
  return null;
}

/** The statuses the stage picker offers for a drop into `column`. */
export function statusesForColumn(column: BoardColumn): readonly ApplicationStatus[] {
  switch (column) {
    case "Applied":
      return ["Applied"];
    case "InProgress":
      return IN_PROGRESS_STATUSES;
    case "Offer":
      return ["Offer"];
    case "Closed":
      return CLOSED_STATUSES;
    default:
      return [];
  }
}

export function canDrop(card: BoardCardResponse, target: BoardColumn): boolean {
  // An application cannot become a saved posting again; a saved posting can go anywhere (it is
  // applied to first).
  return !(card.kind === "Application" && target === "Saved");
}

const DAY_MS = 24 * 60 * 60 * 1000;

export function daysBetween(fromIso: string, now: Date): number {
  return Math.max(0, Math.floor((now.getTime() - new Date(fromIso).getTime()) / DAY_MS));
}

export type CardTone = "fresh" | "quiet" | "silent" | "stale" | "neutral";

export type CardAge =
  | { kind: "saved"; days: number }
  | { kind: "leaves"; days: number }
  | { kind: "moved"; days: number }
  | { kind: "silent"; days: number };

/**
 * The line at the bottom of a card and its colour. Silence is measured from the last activity,
 * never from the application date: the question the board answers is "who has gone quiet", and an
 * old application that heard back yesterday has not.
 */
export function cardAge(card: BoardCardResponse, now: Date, silentDays: number): { age: CardAge; tone: CardTone } {
  if (card.kind === "SavedPosting") {
    return { age: { kind: "saved", days: daysBetween(card.lastActivityAt, now) }, tone: "neutral" };
  }
  if (card.leavesBoardAt) {
    // Rounded, not ceiled: the screen's "now" is when the board loaded, a few seconds before a card
    // was closed, and 14 days plus seconds must still read as 14.
    const days = Math.max(0, Math.round((new Date(card.leavesBoardAt).getTime() - now.getTime()) / DAY_MS));
    return { age: { kind: "leaves", days }, tone: "neutral" };
  }
  const days = daysBetween(card.lastActivityAt, now);
  if (days < 7) return { age: { kind: "moved", days }, tone: "fresh" };
  if (days >= 30) return { age: { kind: "silent", days }, tone: "stale" };
  if (days >= silentDays) return { age: { kind: "silent", days }, tone: "silent" };
  return { age: { kind: "silent", days }, tone: "quiet" };
}

/** Origins that carry a visible mark; Seed and Manual are the user's own doing. */
export function isArrival(origin: BoardCardOrigin): boolean {
  return origin !== "Seed" && origin !== "Manual";
}

// --- Sources -------------------------------------------------------------------------------------

export type SourceGroup = "linkedIn" | "kariyerNet" | "otherSites" | "manual";

export const SOURCE_GROUPS: Record<SourceGroup, readonly Source[]> = {
  linkedIn: ["LinkedIn", "LinkedInImport"],
  kariyerNet: ["KariyerNet"],
  otherSites: [
    "CompanyWebsite",
    "Greenhouse",
    "Lever",
    "Ashby",
    "Workday",
    "Workable",
    "SmartRecruiters",
    "BrowserExtension",
    "Referral",
    "Email",
    "Other",
  ],
  manual: ["Manual", "CsvImport", "System"],
};

export const SOURCE_GROUP_ORDER: readonly SourceGroup[] = ["linkedIn", "kariyerNet", "otherSites", "manual"];

export function sourceGroupOf(source: Source | null): SourceGroup | null {
  if (!source) return null;
  return SOURCE_GROUP_ORDER.find((group) => SOURCE_GROUPS[group].includes(source)) ?? "otherSites";
}

// --- Filter state in the URL ---------------------------------------------------------------------

export const ACTIVITY_WINDOWS = [7, 30, 90] as const;
export type ActivityWindow = (typeof ACTIVITY_WINDOWS)[number];

/**
 * Everything the drawer and the toolbar can set, as it sits in the URL — so a reload, the back
 * button and a shared link keep the board narrowed the same way, like the list's filters.
 */
export interface BoardFilterState {
  search: string;
  sourceGroups: SourceGroup[];
  window: ActivityWindow | null;
  /** yyyy-mm-dd, inclusive; only used while `window` is null. */
  from: string;
  to: string;
  silent: boolean;
  reminder: boolean;
  promise: boolean;
  unseen: boolean;
}

export const EMPTY_BOARD_FILTER: BoardFilterState = {
  search: "",
  sourceGroups: [],
  window: null,
  from: "",
  to: "",
  silent: false,
  reminder: false,
  promise: false,
  unseen: false,
};

const DATE = /^\d{4}-\d{2}-\d{2}$/;

export function parseBoardFilter(params: URLSearchParams): BoardFilterState {
  const windowValue = Number(params.get("active"));
  const groups = (params.get("src") ?? "")
    .split(",")
    .filter((value): value is SourceGroup => (SOURCE_GROUP_ORDER as readonly string[]).includes(value));
  const from = params.get("from") ?? "";
  const to = params.get("to") ?? "";
  return {
    search: params.get("search") ?? "",
    sourceGroups: SOURCE_GROUP_ORDER.filter((group) => groups.includes(group)),
    window: (ACTIVITY_WINDOWS as readonly number[]).includes(windowValue) ? (windowValue as ActivityWindow) : null,
    from: DATE.test(from) ? from : "",
    to: DATE.test(to) ? to : "",
    silent: params.get("silent") === "1",
    reminder: params.get("reminder") === "1",
    promise: params.get("promise") === "1",
    unseen: params.get("unseen") === "1",
  };
}

/** The URL parameters a filter state writes; null removes one. */
export function boardFilterParams(state: BoardFilterState): Record<string, string | null> {
  return {
    search: state.search || null,
    src: state.sourceGroups.length > 0 ? state.sourceGroups.join(",") : null,
    active: state.window ? String(state.window) : null,
    from: state.window ? null : state.from || null,
    to: state.window ? null : state.to || null,
    silent: state.silent ? "1" : null,
    reminder: state.reminder ? "1" : null,
    promise: state.promise ? "1" : null,
    unseen: state.unseen ? "1" : null,
  };
}

/** The request the state stands for. `now` fixes what "the last 30 days" means for this read. */
export function toBoardFilter(state: BoardFilterState, now: Date): BoardFilter {
  let activeFrom: string | undefined;
  let activeTo: string | undefined;
  if (state.window) {
    activeFrom = new Date(now.getTime() - state.window * DAY_MS).toISOString();
  } else {
    if (state.from) activeFrom = new Date(`${state.from}T00:00:00`).toISOString();
    if (state.to) activeTo = new Date(`${state.to}T23:59:59.999`).toISOString();
  }
  return {
    search: state.search.trim() || undefined,
    sources: state.sourceGroups.flatMap((group) => SOURCE_GROUPS[group]),
    activeFrom,
    activeTo,
    silentOnly: state.silent || undefined,
    withReminder: state.reminder || undefined,
    withPromise: state.promise || undefined,
    unseenOnly: state.unseen || undefined,
  };
}

/** How many drawer filters are on — the badge on the "Filters" button. Search has its own box. */
export function activeFilterCount(state: BoardFilterState): number {
  return (
    (state.sourceGroups.length > 0 ? 1 : 0) +
    (state.window || state.from || state.to ? 1 : 0) +
    [state.silent, state.reminder, state.promise, state.unseen].filter(Boolean).length
  );
}

// --- Local column state and moves ----------------------------------------------------------------

export interface ColumnState {
  column: BoardColumn;
  total: number;
  cards: BoardCardResponse[];
  nextCursor: string | null;
}

export function toColumnStates(pages: BoardColumnPage[]): ColumnState[] {
  return BOARD_COLUMNS.map((column) => {
    const page = pages.find((p) => p.column === column);
    return { column, total: page?.total ?? 0, cards: page?.cards ?? [], nextCursor: page?.nextCursor ?? null };
  });
}

export function appendPage(columns: ColumnState[], page: BoardColumnPage): ColumnState[] {
  return columns.map((state) => {
    if (state.column !== page.column) return state;
    const known = new Set(state.cards.map((card) => card.id));
    return {
      ...state,
      total: page.total,
      cards: [...state.cards, ...page.cards.filter((card) => !known.has(card.id))],
      nextCursor: page.nextCursor,
    };
  });
}

export function findCard(columns: ColumnState[], cardId: string): { column: BoardColumn; index: number; card: BoardCardResponse } | null {
  for (const state of columns) {
    const index = state.cards.findIndex((card) => card.id === cardId);
    if (index >= 0) return { column: state.column, index, card: state.cards[index] };
  }
  return null;
}

/**
 * The board after moving a card to `index` in `target` — what the screen shows while the request
 * runs. A move into a new column changes what the card is (its status, or from saved posting to
 * application), exactly as the server will.
 */
export function applyMove(
  columns: ColumnState[],
  cardId: string,
  target: BoardColumn,
  index: number,
  newStatus: ApplicationStatus | null,
  now: Date = new Date(),
  closedVisibleDays = 14,
): ColumnState[] {
  const found = findCard(columns, cardId);
  if (!found) return columns;

  const moved: BoardCardResponse = {
    ...found.card,
    unseen: false,
    // A status change is activity: the card stops reading as silent the moment it moves.
    ...(newStatus
      ? {
          status: newStatus,
          kind: "Application" as const,
          lastActivityAt: now.toISOString(),
          // A card moved into "Closed" starts its countdown now, as the server's ClosedAt does.
          leavesBoardAt:
            columnFor(newStatus) === "Closed"
              ? new Date(now.getTime() + closedVisibleDays * DAY_MS).toISOString()
              : null,
        }
      : {}),
  };

  return columns.map((state) => {
    let cards = state.cards;
    let total = state.total;
    if (state.column === found.column) {
      cards = cards.filter((card) => card.id !== cardId);
      total -= 1;
    }
    if (state.column === target) {
      const at = Math.max(0, Math.min(index, cards.length));
      cards = [...cards.slice(0, at), moved, ...cards.slice(at)];
      total += 1;
    }
    return cards === state.cards ? state : { ...state, cards, total };
  });
}

export function removeCard(columns: ColumnState[], cardId: string): ColumnState[] {
  return columns.map((state) =>
    state.cards.some((card) => card.id === cardId)
      ? { ...state, cards: state.cards.filter((card) => card.id !== cardId), total: state.total - 1 }
      : state,
  );
}

/** The neighbours the server needs to put a card where it now is in `cards`. */
export function neighboursOf(cards: BoardCardResponse[], cardId: string): { aboveCardId: string | null; belowCardId: string | null } {
  const index = cards.findIndex((card) => card.id === cardId);
  return {
    aboveCardId: index > 0 ? cards[index - 1].id : null,
    belowCardId: index >= 0 && index < cards.length - 1 ? cards[index + 1].id : null,
  };
}

// --- Company mark --------------------------------------------------------------------------------

/** Up to two letters for a company's tile: the first letters of its first two words. */
export function companyInitials(name: string): string {
  const words = name
    .trim()
    .split(/\s+/)
    .filter((word) => /\p{L}|\p{N}/u.test(word));
  if (words.length === 0) return "?";
  const first = Array.from(words[0]);
  const letters = words.length > 1 ? [first[0], Array.from(words[1])[0]] : first.slice(0, 2);
  return letters.join("").toLocaleUpperCase("tr");
}

export const COMPANY_TINT_COUNT = 6;

/** A stable tint per company, so the same employer keeps its colour across cards and visits. */
export function companyTint(companyId: string): number {
  let hash = 0;
  for (let i = 0; i < companyId.length; i++) {
    hash = (hash * 31 + companyId.charCodeAt(i)) >>> 0;
  }
  return hash % COMPANY_TINT_COUNT;
}
