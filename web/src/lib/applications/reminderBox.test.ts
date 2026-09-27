import { describe, expect, it } from "vitest";
import { buildReminderBox, type EarlierApplication, type ReminderBoxInput } from "./reminderBox";

const now = new Date(2026, 8, 27, 10, 0);
const local = (y: number, m: number, d: number, h = 12) => new Date(y, m - 1, d, h).toISOString();

const base: ReminderBoxInput = { id: "a", status: "Applied", appliedAt: local(2026, 9, 23) };

describe("buildReminderBox", () => {
  it("is null when there is nothing to remind", () => {
    expect(buildReminderBox(base, [], now)).toBeNull();
  });

  it("measures a waiting application against the user's usual reply time", () => {
    const box = buildReminderBox({ ...base, userMedianResponseDays: 9 }, [], now)!;
    expect(box.patience).toEqual({ day: 4, median: 9, early: true, progress: 4 / 9 });
  });

  it("says it is no longer early once the usual wait has passed", () => {
    const box = buildReminderBox({ ...base, appliedAt: local(2026, 9, 10), userMedianResponseDays: 9 }, [], now)!;
    expect(box.patience).toMatchObject({ day: 17, early: false, progress: 1 });
  });

  it("shows no patience line once there was an answer", () => {
    expect(buildReminderBox({ ...base, status: "Screening", userMedianResponseDays: 9 }, [], now)).toBeNull();
  });

  it("gives the posting's age on the day of applying, from one day up", () => {
    expect(buildReminderBox({ ...base, jobPublishedAt: local(2026, 8, 31) }, [], now)!.postingAgeDays).toBe(23);
    expect(buildReminderBox({ ...base, jobPublishedAt: local(2026, 9, 23, 8) }, [], now)).toBeNull();
  });

  it("tells a waiting user the posting closed, and steps the patience line aside", () => {
    const box = buildReminderBox({ ...base, userMedianResponseDays: 9, jobClosedAt: local(2026, 9, 22) }, [], now)!;
    expect(box.closed).toEqual({ daysAgo: 5, noReply: true });
    expect(box.patience).toBeNull();
  });

  it("still says it closed mid-process, without claiming silence", () => {
    const box = buildReminderBox({ ...base, status: "Interview", jobClosedAt: local(2026, 9, 26) }, [], now)!;
    expect(box.closed).toEqual({ daysAgo: 1, noReply: false });
  });

  it("says nothing about a closed posting once the process has ended", () => {
    expect(buildReminderBox({ ...base, status: "Rejected", jobClosedAt: local(2026, 9, 22) }, [], now)).toBeNull();
  });

  it("recalls the latest earlier application at the same company, never itself or a later one", () => {
    const others: EarlierApplication[] = [
      { id: "a", jobTitle: "This one", appliedAt: base.appliedAt, status: "Applied" },
      { id: "b", jobTitle: "Older", appliedAt: local(2025, 9, 1), status: "Ghosted" },
      { id: "c", jobTitle: "Frontend Developer", appliedAt: local(2026, 3, 12), status: "Rejected" },
      { id: "d", jobTitle: "Later", appliedAt: local(2026, 9, 25), status: "Applied" },
    ];
    expect(buildReminderBox(base, others, now)!.earlier?.id).toBe("c");
  });
});
