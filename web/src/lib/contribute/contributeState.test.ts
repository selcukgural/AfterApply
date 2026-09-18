import { describe, expect, it } from "vitest";
import { contributeHref, nextSideAfterSave, ownListHref, parseContributeTab, type ContributionContext } from "./contributeState";

const open: ContributionContext = {
  ownReview: false,
  ownSalaryCount: 0,
  ownExperience: false,
  reviewQuotaLeft: 9,
  salaryQuotaLeft: 9,
  experienceQuotaLeft: 9,
  reviewsEnabled: true,
  salariesEnabled: true,
  experiencesEnabled: true,
};

/** The page as it was before the third side: experiences off. */
const twoSided: ContributionContext = { ...open, experiencesEnabled: false };

describe("parseContributeTab", () => {
  it("opens the review side for anything but a known side", () => {
    expect(parseContributeTab("salary")).toBe("salary");
    expect(parseContributeTab("experience")).toBe("experience");
    expect(parseContributeTab("review")).toBe("review");
    expect(parseContributeTab(null)).toBe("review");
    expect(parseContributeTab("SALARY")).toBe("review");
    expect(parseContributeTab("other")).toBe("review");
  });
});

describe("nextSideAfterSave", () => {
  it("invites a review after a salary when there is none yet", () => {
    expect(nextSideAfterSave("salary", twoSided)).toBe("review");
    expect(nextSideAfterSave("salary", open)).toBe("review");
  });

  it("invites a salary after a review while quota remains, even with entries already there", () => {
    expect(nextSideAfterSave("review", twoSided)).toBe("salary");
    expect(nextSideAfterSave("review", { ...twoSided, ownSalaryCount: 2 })).toBe("salary");
  });

  it("prefers the first side the author has not used at this company", () => {
    expect(nextSideAfterSave("review", open)).toBe("salary");
    expect(nextSideAfterSave("review", { ...open, ownSalaryCount: 1 })).toBe("experience");
    expect(nextSideAfterSave("experience", open)).toBe("review");
    expect(nextSideAfterSave("experience", { ...open, ownReview: true })).toBe("salary");
    expect(nextSideAfterSave("salary", { ...open, ownReview: true })).toBe("experience");
  });

  it("falls back to another salary when every fresh side is taken, else to the own list", () => {
    expect(nextSideAfterSave("review", { ...open, ownSalaryCount: 1, ownExperience: true })).toBe("salary");
    expect(nextSideAfterSave("salary", { ...open, ownReview: true, ownExperience: true })).toBeNull();
    expect(nextSideAfterSave("review", { ...open, ownSalaryCount: 1, ownExperience: true, salaryQuotaLeft: 0 })).toBeNull();
  });

  it("skips a side that is off, full or already used", () => {
    expect(nextSideAfterSave("salary", { ...open, ownReview: true, experiencesEnabled: false })).toBeNull();
    expect(nextSideAfterSave("salary", { ...open, ownReview: true, experienceQuotaLeft: 0 })).toBeNull();
    expect(nextSideAfterSave("salary", { ...twoSided, reviewQuotaLeft: 0 })).toBeNull();
    expect(nextSideAfterSave("salary", { ...twoSided, reviewsEnabled: false })).toBeNull();
    expect(nextSideAfterSave("review", { ...twoSided, salaryQuotaLeft: 0 })).toBeNull();
    expect(nextSideAfterSave("review", { ...twoSided, salariesEnabled: false })).toBeNull();
  });
});

describe("contributeHref", () => {
  it("carries the company when one is chosen", () => {
    expect(contributeHref("review")).toBe("/contribute?tab=review");
    expect(contributeHref("salary", "beta-a-s")).toBe("/contribute?tab=salary&company=beta-a-s");
    expect(contributeHref("experience", "beta-a-s")).toBe("/contribute?tab=experience&company=beta-a-s");
    expect(contributeHref("salary", null)).toBe("/contribute?tab=salary");
  });
});

describe("ownListHref", () => {
  it("names the one contributions list for every side (2026-09-18)", () => {
    expect(ownListHref("review")).toBe("/my-reviews");
    expect(ownListHref("salary")).toBe("/my-reviews");
    expect(ownListHref("experience")).toBe("/my-reviews");
  });
});
