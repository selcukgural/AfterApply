import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

const read = (file: string) => readFileSync(path.join(process.cwd(), "src", file), "utf8");
const TYPES = ["ReviewHelpful", "SalaryHelpful", "ExperienceHelpful", "BlogCommentHelpful"] as const;

/**
 * Contribution notifications (DECISIONS.md 2026-09-23, canvas variants A + C1 + D). No component
 * harness here, so the decisions that matter are pinned on the source and the catalogues.
 */
describe("contribution notifications", () => {
  it("say how many, never who, for every kind in both languages", () => {
    for (const catalogue of [tr, en]) {
      for (const type of TYPES) {
        const sentence = catalogue.notifications.contribution[type];
        expect(sentence).toContain("{count, plural");
        expect(sentence).not.toMatch(/\{(name|voter|user|who)\}/);
        expect(catalogue.notifications.contributionLink[type]).not.toBe("");
      }
    }
  });

  it("render the date and the time on the panel and the page alike", () => {
    expect(read("components/notifications/NotificationBell.tsx")).toContain("formatNotificationTime(item.occurredAt, locale)");
    expect(read("components/notifications/NotificationCard.tsx")).toContain("formatNotificationTime(item.occurredAt, locale)");
  });

  it("keep their settings in Account settings, where the bell's gear and the page link land", () => {
    const settings = read("app/[locale]/(protected)/settings/page.tsx");
    expect(settings).toContain("<NotificationSettingsCard />");
    expect(read("components/settings/NotificationSettingsCard.tsx")).toContain('id="notifications"');
    expect(read("app/[locale]/(protected)/notifications/page.tsx")).toContain('href="/settings#notifications"');
    // Not on the profile page (variant E was not chosen).
    expect(read("app/[locale]/(protected)/profile/page.tsx")).not.toContain("NotificationSettingsCard");
  });

  it("offer one helpful pill for reviews, salaries and experiences", () => {
    expect(read("components/companyReviews/ReviewCard.tsx")).toContain("<HelpfulPill");
    expect(read("components/companySalaries/CompanySalariesPanel.tsx")).toContain("<HelpfulPill");
    expect(read("components/candidateExperiences/CandidateExperiencesPanel.tsx")).toContain("<HelpfulPill");
  });
});
