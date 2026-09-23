import { describe, expect, it } from "vitest";
import type { NotificationPreferences } from "@/types/api";
import { CONTRIBUTION_PREFERENCE_KEYS, effectiveSwitch, toggled } from "./preferences";

const allOn: NotificationPreferences = {
  contributions: true,
  reviewHelpful: true,
  salaryHelpful: true,
  experienceHelpful: true,
  blogCommentHelpful: true,
  gmailUpdates: true,
};

describe("effectiveSwitch", () => {
  it("shows a kind's own value while the master is on", () => {
    expect(effectiveSwitch({ ...allOn, salaryHelpful: false }, "salaryHelpful")).toEqual({ on: false, locked: false });
    expect(effectiveSwitch(allOn, "reviewHelpful")).toEqual({ on: true, locked: false });
  });

  it("shows every kind off and locked while the master is off", () => {
    for (const key of CONTRIBUTION_PREFERENCE_KEYS) {
      expect(effectiveSwitch({ ...allOn, contributions: false }, key)).toEqual({ on: false, locked: true });
    }
  });
});

describe("toggled", () => {
  it("flips one switch and keeps the rest, the master included", () => {
    expect(toggled(allOn, "experienceHelpful")).toEqual({ ...allOn, experienceHelpful: false });
  });

  it("keeps each kind's saved value when the master goes off and back on", () => {
    const saved = { ...allOn, blogCommentHelpful: false };
    const off = toggled(saved, "contributions");
    expect(off.blogCommentHelpful).toBe(false);
    expect(off.reviewHelpful).toBe(true);
    expect(toggled(off, "contributions")).toEqual(saved);
  });
});

describe("CONTRIBUTION_PREFERENCE_KEYS", () => {
  it("lists the four kinds in the settings card's order", () => {
    expect(CONTRIBUTION_PREFERENCE_KEYS).toEqual(["reviewHelpful", "salaryHelpful", "experienceHelpful", "blogCommentHelpful"]);
  });
});
