import type { ApplicationFlowCounts, ApplicationFlowResponse } from "@/types/api";

/**
 * The shareable flow card (2026-09-23): what a "share your flow" link carries in its URL, and
 * nothing else.
 *
 * `86-47-0-21-0-0-18-2-4-9-3-0-11-202606-202609` is the twelve node counts in `FLOW_FIELDS` order,
 * the median days to a first reply (`n` when nothing was answered) and the first and last month the
 * card covers. Numbers only, by the same rule as the CV score card (lib/cvScan/scoreCard.ts): no
 * company, role or single application is in the address, and a card is exactly as public as the
 * person who pasted it chose to make it.
 *
 * A card whose columns do not add up is refused rather than drawn, so a hand-edited URL cannot put
 * a flow on the site's card that no set of applications could have produced. Nor can one below
 * `MIN_FLOW_CARD_TOTAL`: a handful of applications is not a picture of anything, and the fewer
 * there are, the easier each one is to recognise.
 */

export const FLOW_FIELDS = [
  "total",
  "unanswered",
  "awaitingReply",
  "rejectedBeforeInterview",
  "inScreening",
  "withdrawnBeforeInterview",
  "interviewed",
  "offer",
  "interviewInProgress",
  "rejectedAfterInterview",
  "silentAfterInterview",
  "withdrawnAfterInterview",
] as const satisfies readonly (keyof ApplicationFlowCounts)[];

/** The first-column nodes, top to bottom. `interviewed` is last: its flow continues right. */
export const FIRST_COLUMN = [
  "unanswered",
  "awaitingReply",
  "rejectedBeforeInterview",
  "inScreening",
  "withdrawnBeforeInterview",
  "interviewed",
] as const satisfies readonly (keyof ApplicationFlowCounts)[];

/** The after-interview nodes, top to bottom. */
export const SECOND_COLUMN = [
  "offer",
  "interviewInProgress",
  "rejectedAfterInterview",
  "silentAfterInterview",
  "withdrawnAfterInterview",
] as const satisfies readonly (keyof ApplicationFlowCounts)[];

export const MIN_FLOW_CARD_TOTAL = 10;
const MAX_COUNT = 100_000;
const MAX_MEDIAN_DAYS = 3650;

export interface YearMonth {
  year: number;
  /** 1–12. */
  month: number;
}

export interface FlowCard {
  counts: ApplicationFlowCounts;
  /** Whole days; null when nothing in the window was answered. */
  medianDays: number | null;
  from: YearMonth;
  to: YearMonth;
}

/** What the card calls "left unanswered": never heard back, or went quiet after an interview. */
export function unansweredTotal(counts: ApplicationFlowCounts): number {
  return counts.unanswered + counts.silentAfterInterview;
}

export function isShareable(counts: ApplicationFlowCounts): boolean {
  return counts.total >= MIN_FLOW_CARD_TOTAL;
}

function yearMonthOf(isoDate: string): YearMonth {
  const [year, month] = isoDate.split("-").map(Number);
  return { year, month };
}

/** The card for an API response. The caller checks `isShareable` first. */
export function flowCardFromResponse(response: ApplicationFlowResponse): FlowCard {
  return {
    counts: response.counts,
    medianDays: response.medianFirstReplyDays === null ? null : Math.max(0, Math.round(response.medianFirstReplyDays)),
    from: yearMonthOf(response.firstAppliedOn ?? response.today),
    to: yearMonthOf(response.today),
  };
}

const pad2 = (value: number) => String(value).padStart(2, "0");
const formatYearMonth = ({ year, month }: YearMonth) => `${year}${pad2(month)}`;

/** The URL segment for a card. */
export function formatFlowCard(card: FlowCard): string {
  return [
    ...FLOW_FIELDS.map((field) => card.counts[field]),
    card.medianDays === null ? "n" : card.medianDays,
    formatYearMonth(card.from),
    formatYearMonth(card.to),
  ].join("-");
}

const SEGMENT_PATTERN = new RegExp(`^(\\d{1,6}-){${FLOW_FIELDS.length}}(n|\\d{1,4})-(\\d{6})-(\\d{6})$`);

function parseYearMonth(value: string): YearMonth | null {
  const year = Number(value.slice(0, 4));
  const month = Number(value.slice(4));
  if (year < 2000 || year > 2100 || month < 1 || month > 12) return null;
  return { year, month };
}

const monthIndex = ({ year, month }: YearMonth) => year * 12 + (month - 1);

/** Reads a card back, or null when it is not one a real set of applications could have produced. */
export function parseFlowCard(segment: string): FlowCard | null {
  if (!SEGMENT_PATTERN.test(segment)) return null;

  const parts = segment.split("-");
  const numbers = parts.slice(0, FLOW_FIELDS.length).map(Number);
  if (numbers.some((value) => value > MAX_COUNT)) return null;

  const counts = Object.fromEntries(FLOW_FIELDS.map((field, index) => [field, numbers[index]])) as unknown as ApplicationFlowCounts;
  if (counts.total < MIN_FLOW_CARD_TOTAL) return null;
  if (FIRST_COLUMN.reduce((sum, field) => sum + counts[field], 0) !== counts.total) return null;
  if (SECOND_COLUMN.reduce((sum, field) => sum + counts[field], 0) !== counts.interviewed) return null;

  const medianPart = parts[FLOW_FIELDS.length];
  const medianDays = medianPart === "n" ? null : Number(medianPart);
  if (medianDays !== null && medianDays > MAX_MEDIAN_DAYS) return null;

  const from = parseYearMonth(parts[FLOW_FIELDS.length + 1]);
  const to = parseYearMonth(parts[FLOW_FIELDS.length + 2]);
  if (!from || !to || monthIndex(from) > monthIndex(to)) return null;

  return { counts, medianDays, from, to };
}

/**
 * "Haziran – Eylül 2026", "Eylül 2026", "Kasım 2025 – Eylül 2026". Month names from Intl, first
 * letter upper-cased the locale's way (Turkish month names are capitalised, but not every runtime's
 * ICU agrees).
 */
export function formatMonthRange(from: YearMonth, to: YearMonth, locale: string): string {
  const monthName = ({ year, month }: YearMonth) => {
    const name = new Intl.DateTimeFormat(locale, { month: "long", timeZone: "UTC" }).format(
      new Date(Date.UTC(year, month - 1, 15)),
    );
    return name.charAt(0).toLocaleUpperCase(locale) + name.slice(1);
  };

  if (from.year === to.year && from.month === to.month) return `${monthName(to)} ${to.year}`;
  if (from.year === to.year) return `${monthName(from)} – ${monthName(to)} ${to.year}`;
  return `${monthName(from)} ${from.year} – ${monthName(to)} ${to.year}`;
}
