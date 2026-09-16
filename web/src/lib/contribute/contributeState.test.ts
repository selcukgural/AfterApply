import { describe, expect, it } from "vitest";
import { contributeHref, nextSideAfterSave, parseContributeTab, type ContributionContext } from "./contributeState";

const open: ContributionContext = {
  ownReview: false,
  ownSalaryCount: 0,
  reviewQuotaLeft: 9,
  salaryQuotaLeft: 9,
  reviewsEnabled: true,
  salariesEnabled: true,
};

describe("parseContributeTab", () => {
  it("opens the review side for anything but salary", () => {
    expect(parseContributeTab("salary")).toBe("salary");
    expect(parseContributeTab("review")).toBe("review");
    expect(parseContributeTab(null)).toBe("review");
    expect(parseContributeTab("SALARY")).toBe("review");
    expect(parseContributeTab("other")).toBe("review");
  });
});

describe("nextSideAfterSave", () => {
  it("invites a review after a salary when there is none yet", () => {
    expect(nextSideAfterSave("salary", open)).toBe("review");
  });

  it("invites a salary after a review while quota remains, even with entries already there", () => {
    expect(nextSideAfterSave("review", open)).toBe("salary");
    expect(nextSideAfterSave("review", { ...open, ownSalaryCount: 2 })).toBe("salary");
  });

  it("falls back to the own list when the other side is done, full or off", () => {
    expect(nextSideAfterSave("salary", { ...open, ownReview: true })).toBeNull();
    expect(nextSideAfterSave("salary", { ...open, reviewQuotaLeft: 0 })).toBeNull();
    expect(nextSideAfterSave("salary", { ...open, reviewsEnabled: false })).toBeNull();
    expect(nextSideAfterSave("review", { ...open, salaryQuotaLeft: 0 })).toBeNull();
    expect(nextSideAfterSave("review", { ...open, salariesEnabled: false })).toBeNull();
  });
});

describe("contributeHref", () => {
  it("carries the company when one is chosen", () => {
    expect(contributeHref("review")).toBe("/contribute?tab=review");
    expect(contributeHref("salary", "beta-a-s")).toBe("/contribute?tab=salary&company=beta-a-s");
    expect(contributeHref("salary", null)).toBe("/contribute?tab=salary");
  });
});
