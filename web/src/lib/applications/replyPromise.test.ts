import { describe, expect, it } from "vitest";
import {
  asksForPromise,
  asksForRejectionNotice,
  daysUntil,
  formatPromiseDate,
  isClosedStatus,
  promiseDateBounds,
  promiseLine,
  REJECTION_NOTICE_OPTIONS,
  todayDateOnly,
} from "./replyPromise";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";

describe("replyPromise", () => {
  it("asks for a date only for a status still in play, and matches the server's closed set", () => {
    const open = APPLICATION_STATUSES.filter(asksForPromise);
    expect(open).toEqual(["Applied", "Screening", "Interview", "TechnicalInterview", "FinalInterview", "Offer"]);
    expect(APPLICATION_STATUSES.filter(isClosedStatus)).toEqual(["Accepted", "Rejected", "Withdrawn", "Ghosted"]);
  });

  it("asks how the rejection was learned of only for Rejected", () => {
    expect(APPLICATION_STATUSES.filter(asksForRejectionNotice)).toEqual(["Rejected"]);
    expect(REJECTION_NOTICE_OPTIONS).toEqual(["CompanyNotified", "SeenOnPortal", "OtherOrInferred"]);
  });

  it("formats a calendar date without shifting it through UTC", () => {
    expect(formatPromiseDate("2026-10-10", "tr-TR")).toBe("10.10.2026");
    expect(formatPromiseDate("2026-01-01", "en-US")).toBe("1/1/2026");
    expect(formatPromiseDate("not a date", "tr-TR")).toBe("not a date");
  });

  it("counts whole calendar days either way, across a month end", () => {
    expect(daysUntil("2026-10-10", "2026-10-06")).toBe(4);
    expect(daysUntil("2026-10-10", "2026-10-10")).toBe(0);
    expect(daysUntil("2026-10-10", "2026-10-13")).toBe(-3);
    expect(daysUntil("2026-11-02", "2026-10-30")).toBe(3);
  });

  it("reads today in the local calendar as YYYY-MM-DD", () => {
    expect(todayDateOnly(new Date(2026, 8, 3, 23, 59))).toBe("2026-09-03");
  });

  it("bounds the date input to a year either side, like the server", () => {
    expect(promiseDateBounds("2026-09-23")).toEqual({ min: "2025-09-23", max: "2027-09-23" });
  });

  it("says days left, due today, overdue, kept or late from the server's outcome", () => {
    expect(promiseLine("Pending", "2026-10-10", "2026-10-06")).toEqual({ key: "daysLeft", count: 4, tone: "neutral" });
    expect(promiseLine("Pending", "2026-10-10", "2026-10-10")).toEqual({ key: "dueToday", count: 0, tone: "neutral" });
    expect(promiseLine("Overdue", "2026-10-10", "2026-10-13")).toEqual({ key: "overdue", count: 3, tone: "warn" });
    // The reader's calendar may still be on the date while the server's is past it.
    expect(promiseLine("Overdue", "2026-10-10", "2026-10-10")).toEqual({ key: "overdue", count: 1, tone: "warn" });
    expect(promiseLine("Kept", "2026-10-10", "2026-10-20")).toEqual({ key: "kept", count: 0, tone: "good" });
    expect(promiseLine("Late", "2026-10-10", "2026-10-20")).toEqual({ key: "late", count: 0, tone: "warn" });
    expect(promiseLine("Void", "2026-10-10", "2026-10-20")).toBeNull();
  });
});
