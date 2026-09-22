import { describe, expect, it } from "vitest";
import { EMPTY_SELECTION, selectAllMatching } from "@/lib/applications/bulkSelection";
import { REMINDER_ANSWER_KEY, REMINDER_LABEL_KEY, answersByGhosting, clampPage, lastPage, toReminderSelection } from "./reminders";

describe("REMINDER_LABEL_KEY", () => {
  it("covers every reminder type", () => {
    expect(REMINDER_LABEL_KEY.FollowUp).toBe("followUp");
    expect(REMINDER_LABEL_KEY.PossiblyGhosted).toBe("possiblyGhosted");
    expect(REMINDER_LABEL_KEY.PromiseMissed).toBe("promiseMissed");
  });
});

describe("REMINDER_ANSWER_KEY", () => {
  it("gives each reminder type the answer its question asks for", () => {
    expect(REMINDER_ANSWER_KEY.FollowUp).toBe("followedUp");
    expect(REMINDER_ANSWER_KEY.PossiblyGhosted).toBe("markGhosted");
    // A missed date is a reason to write, not to close.
    expect(REMINDER_ANSWER_KEY.PromiseMissed).toBe("followedUp");
  });

  it("changes a status only for the ghosting question", () => {
    expect(answersByGhosting("PossiblyGhosted")).toBe(true);
    expect(answersByGhosting("FollowUp")).toBe(false);
    expect(answersByGhosting("PromiseMissed")).toBe(false);
  });
});

describe("toReminderSelection", () => {
  it("sends the ticked ids as they are", () => {
    expect(toReminderSelection({ kind: "page", ids: ["a", "b"] })).toEqual({ ids: ["a", "b"] });
  });

  it("sends an empty page selection as an empty id list, never as all", () => {
    // The caller guards against submitting nothing; if it ever slips through, the server's
    // validator rejects an empty list — an accidental "all" would not be rejected.
    expect(toReminderSelection(EMPTY_SELECTION)).toEqual({ ids: [] });
  });

  it("sends all-matching as the all flag without ids", () => {
    expect(toReminderSelection(selectAllMatching(1224))).toEqual({ all: true });
  });
});

describe("lastPage", () => {
  it("rounds up and never goes below one", () => {
    expect(lastPage(0, 5)).toBe(1);
    expect(lastPage(5, 5)).toBe(1);
    expect(lastPage(6, 5)).toBe(2);
    expect(lastPage(1224, 5)).toBe(245);
  });
});

describe("clampPage", () => {
  it("keeps a page that still has rows", () => {
    expect(clampPage(3, 1224, 5)).toBe(3);
    expect(clampPage(245, 1224, 5)).toBe(245);
  });

  it("falls back to the last page once the list has shrunk under the current one", () => {
    expect(clampPage(245, 1220, 5)).toBe(244);
    expect(clampPage(2, 0, 5)).toBe(1);
  });
});
