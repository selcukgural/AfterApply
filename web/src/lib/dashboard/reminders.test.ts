import { describe, expect, it } from "vitest";
import type { ReminderResponse } from "@/types/api";
import { REMINDER_LABEL_KEY, sortReminders } from "./reminders";

const reminder = (overrides: Partial<ReminderResponse>): ReminderResponse => ({
  id: "r",
  applicationId: "a",
  companyName: "Acme",
  jobTitle: "Engineer",
  type: "FollowUp",
  daysElapsed: 10,
  createdAt: "2026-09-10T00:00:00Z",
  ...overrides,
});

describe("sortReminders", () => {
  it("puts the application that has waited longest first", () => {
    const sorted = sortReminders([
      reminder({ id: "fresh", daysElapsed: 7 }),
      reminder({ id: "stale", daysElapsed: 21, type: "PossiblyGhosted" }),
      reminder({ id: "mid", daysElapsed: 14 }),
    ]);
    expect(sorted.map((r) => r.id)).toEqual(["stale", "mid", "fresh"]);
  });

  it("breaks ties by creation time, oldest first", () => {
    const sorted = sortReminders([
      reminder({ id: "later", createdAt: "2026-09-12T00:00:00Z" }),
      reminder({ id: "earlier", createdAt: "2026-09-11T00:00:00Z" }),
    ]);
    expect(sorted.map((r) => r.id)).toEqual(["earlier", "later"]);
  });

  it("does not mutate its input", () => {
    const input = [reminder({ id: "a", daysElapsed: 1 }), reminder({ id: "b", daysElapsed: 2 })];
    sortReminders(input);
    expect(input.map((r) => r.id)).toEqual(["a", "b"]);
  });
});

describe("REMINDER_LABEL_KEY", () => {
  it("covers every reminder type the API can send", () => {
    expect(Object.keys(REMINDER_LABEL_KEY).sort()).toEqual(["FollowUp", "PossiblyGhosted"]);
  });
});
