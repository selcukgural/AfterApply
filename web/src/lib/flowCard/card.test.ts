import { describe, expect, it } from "vitest";
import type { ApplicationFlowCounts } from "@/types/api";
import {
  FIRST_COLUMN,
  FLOW_FIELDS,
  MIN_FLOW_CARD_TOTAL,
  SECOND_COLUMN,
  flowCardFromResponse,
  formatFlowCard,
  formatMonthRange,
  isShareable,
  parseFlowCard,
  unansweredTotal,
} from "./card";

// The canvas's example: 86 applications, 47 unanswered, 21 rejected early, 18 interviewed.
const counts: ApplicationFlowCounts = {
  total: 86,
  unanswered: 47,
  awaitingReply: 0,
  rejectedBeforeInterview: 21,
  inScreening: 0,
  withdrawnBeforeInterview: 0,
  interviewed: 18,
  offer: 2,
  interviewInProgress: 4,
  rejectedAfterInterview: 9,
  silentAfterInterview: 3,
  withdrawnAfterInterview: 0,
};
const card = { counts, medianDays: 11, from: { year: 2026, month: 6 }, to: { year: 2026, month: 9 } };
const SEGMENT = "86-47-0-21-0-0-18-2-4-9-3-0-11-202606-202609";

describe("the flow card", () => {
  it("names every count exactly once across the two columns and the total", () => {
    expect(new Set([...FIRST_COLUMN, ...SECOND_COLUMN, "total"])).toEqual(new Set(FLOW_FIELDS));
    expect(FLOW_FIELDS).toHaveLength(12);
  });

  it("writes the counts, the median and the month range", () => {
    expect(formatFlowCard(card)).toBe(SEGMENT);
    expect(formatFlowCard({ ...card, medianDays: null })).toBe("86-47-0-21-0-0-18-2-4-9-3-0-n-202606-202609");
  });

  it("reads its own output back", () => {
    expect(parseFlowCard(SEGMENT)).toEqual(card);
    expect(parseFlowCard(formatFlowCard({ ...card, medianDays: null }))).toEqual({ ...card, medianDays: null });
  });

  it.each([
    ["the first column does not add up to the total", "86-46-0-21-0-0-18-2-4-9-3-0-11-202606-202609"],
    ["the second column does not add up to the interviews", "86-47-0-21-0-0-18-2-4-9-4-0-11-202606-202609"],
    ["too few applications to be a picture of anything", "9-5-0-4-0-0-0-0-0-0-0-0-n-202606-202609"],
    ["a month out of range", "86-47-0-21-0-0-18-2-4-9-3-0-11-202613-202609"],
    ["the range runs backwards", "86-47-0-21-0-0-18-2-4-9-3-0-11-202609-202606"],
    ["a missing field", "86-47-0-21-0-0-18-2-4-9-3-11-202606-202609"],
    ["something other than digits", "86-47-0-21-0-0-18-2-4-9-3-x-11-202606-202609"],
    ["a script", "<script>"],
    ["an empty segment", ""],
  ])("refuses a card when %s", (_reason, segment) => {
    expect(parseFlowCard(segment)).toBeNull();
  });

  it("accepts exactly the minimum", () => {
    expect(parseFlowCard(`${MIN_FLOW_CARD_TOTAL}-${MIN_FLOW_CARD_TOTAL}-0-0-0-0-0-0-0-0-0-0-n-202609-202609`)).not.toBeNull();
  });

  it("counts silence after an interview as unanswered too", () => {
    expect(unansweredTotal(counts)).toBe(50);
  });

  it("is shareable from ten applications", () => {
    expect(isShareable({ ...counts, total: 9 })).toBe(false);
    expect(isShareable({ ...counts, total: 10 })).toBe(true);
  });

  it("builds a card from the API response, rounding the median to whole days", () => {
    expect(
      flowCardFromResponse({ counts, medianFirstReplyDays: 10.6, firstAppliedOn: "2026-06-24", today: "2026-09-23" }),
    ).toEqual(card);
    expect(
      flowCardFromResponse({ counts, medianFirstReplyDays: null, firstAppliedOn: null, today: "2026-09-23" }),
    ).toEqual({ ...card, medianDays: null, from: { year: 2026, month: 9 } });
  });

  it("writes the month range the way the card prints it", () => {
    expect(formatMonthRange({ year: 2026, month: 6 }, { year: 2026, month: 9 }, "tr")).toBe("Haziran – Eylül 2026");
    expect(formatMonthRange({ year: 2026, month: 9 }, { year: 2026, month: 9 }, "tr")).toBe("Eylül 2026");
    expect(formatMonthRange({ year: 2025, month: 11 }, { year: 2026, month: 9 }, "en")).toBe("November 2025 – September 2026");
  });
});
