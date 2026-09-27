import { describe, expect, it } from "vitest";
import {
  asksForInterview,
  buildIcs,
  calendarDaysUntil,
  escapeIcsText,
  foldIcsLine,
  googleCalendarUrl,
  interviewDayKey,
  nextStagesAfter,
  snoozeOptionsFor,
  splitInstant,
  toCalendarStamp,
  toInterviewInstant,
  type CalendarEvent,
} from "./interview";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";

const event: CalendarEvent = {
  uid: "app-1-20260928T110000Z",
  start: "2026-09-28T11:00:00.000Z",
  title: "Teknik Mülakat — Kuzey Yazılım",
  description: "Backend Developer · Online",
};

describe("interview stages", () => {
  it("asks for a date in exactly the server's interview stages", () => {
    expect(APPLICATION_STATUSES.filter(asksForInterview)).toEqual(["Screening", "Interview", "TechnicalInterview", "FinalInterview"]);
  });

  it("offers only the stages after the current one", () => {
    expect(nextStagesAfter("Interview")).toEqual(["TechnicalInterview", "FinalInterview", "Offer", "Accepted"]);
    expect(nextStagesAfter("FinalInterview")).toEqual(["Offer", "Accepted"]);
    // Screening is not in the pipeline list: everything after it is on offer.
    expect(nextStagesAfter("Screening")).toEqual(["Interview", "TechnicalInterview", "FinalInterview", "Offer", "Accepted"]);
    expect(nextStagesAfter(null)).toHaveLength(5);
  });

  it("offers short snoozes on an interview question and longer ones elsewhere", () => {
    expect(snoozeOptionsFor("InterviewHeld")).toEqual([1, 3]);
    expect(snoozeOptionsFor("FollowUp")).toEqual([3, 7, 14]);
    expect(snoozeOptionsFor("PossiblyGhosted")).toEqual([3, 7, 14]);
  });
});

describe("interview instant", () => {
  it("reads the two inputs as the reader's local time and gives them back", () => {
    const iso = toInterviewInstant("2026-09-28", "14:00");
    expect(iso).not.toBeNull();
    expect(splitInstant(iso!)).toEqual({ date: "2026-09-28", time: "14:00" });
  });

  it("refuses an empty or malformed input", () => {
    expect(toInterviewInstant("", "14:00")).toBeNull();
    expect(toInterviewInstant("2026-09-28", "")).toBeNull();
    expect(toInterviewInstant("28.09.2026", "14:00")).toBeNull();
  });

  it("names the day by the reader's calendar, not by 24-hour distance", () => {
    const now = new Date(2026, 8, 27, 23, 30);
    expect(interviewDayKey(new Date(2026, 8, 28, 0, 30).toISOString(), now)).toBe("tomorrow");
    expect(interviewDayKey(new Date(2026, 8, 27, 9, 0).toISOString(), now)).toBe("today");
    expect(interviewDayKey(new Date(2026, 8, 26, 10, 0).toISOString(), now)).toBe("yesterday");
    expect(interviewDayKey(new Date(2026, 9, 2, 10, 0).toISOString(), now)).toBe("onDate");
    expect(calendarDaysUntil(new Date(2026, 9, 2, 10, 0).toISOString(), now)).toBe(5);
  });
});

describe("calendar", () => {
  it("writes UTC stamps without separators or milliseconds", () => {
    expect(toCalendarStamp("2026-09-28T11:00:00.000Z")).toBe("20260928T110000Z");
  });

  it("escapes the user's text the way RFC 5545 asks", () => {
    expect(escapeIcsText("A, B; C\\D\nE")).toBe("A\\, B\\; C\\\\D\\nE");
  });

  it("folds long lines at 75 bytes without splitting a Turkish character", () => {
    const line = `SUMMARY:${"ğ".repeat(60)}`;
    const folded = foldIcsLine(line);
    const encoder = new TextEncoder();
    for (const part of folded.split("\r\n")) {
      expect(encoder.encode(part).length).toBeLessThanOrEqual(75);
    }
    expect(folded.replace(/\r\n /g, "")).toBe(line);
  });

  it("builds a one-hour event with a reminder an hour before", () => {
    const ics = buildIcs(event, new Date("2026-09-27T09:00:00Z"));
    expect(ics).toContain("BEGIN:VCALENDAR\r\n");
    expect(ics).toContain("UID:app-1-20260928T110000Z@ekariyerim.com\r\n");
    expect(ics).toContain("DTSTART:20260928T110000Z\r\n");
    expect(ics).toContain("DTEND:20260928T120000Z\r\n");
    expect(ics).toContain("DTSTAMP:20260927T090000Z\r\n");
    expect(ics).toContain("SUMMARY:Teknik Mülakat — Kuzey Yazılım\r\n");
    expect(ics).toContain("TRIGGER:-PT1H\r\n");
    expect(ics.endsWith("END:VCALENDAR\r\n")).toBe(true);
  });

  it("links to Google's add-event form with the same hour", () => {
    const url = new URL(googleCalendarUrl(event));
    expect(url.origin).toBe("https://calendar.google.com");
    expect(url.searchParams.get("action")).toBe("TEMPLATE");
    expect(url.searchParams.get("dates")).toBe("20260928T110000Z/20260928T120000Z");
    expect(url.searchParams.get("text")).toBe(event.title);
    expect(url.searchParams.get("details")).toBe(event.description);
  });
});
