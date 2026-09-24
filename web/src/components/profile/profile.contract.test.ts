import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";

/**
 * What the profile page promised on the 2026-09-17 canvas (variant A). Source scans, like
 * siteChrome.contract.test.ts: there is no render harness, and each rule is one a screenshot
 * review would otherwise re-check by hand.
 */
const SRC = path.join(process.cwd(), "src");
const read = (relative: string) => readFileSync(path.join(SRC, relative), "utf8");

describe("the profile page", () => {
  const page = read("app/[locale]/(protected)/profile/page.tsx");
  const identity = read("components/profile/ProfileIdentityCard.tsx");
  const plan = read("components/profile/PlanCard.tsx");
  const contributions = read("components/profile/ContributionsCard.tsx");

  it("lets the name be edited and saves it where the navbar reads it", () => {
    expect(identity).toContain("authApi.updateProfile(");
    expect(identity).toContain("authStore.updateUser(profile)");
    expect(identity).toContain('id="profile-firstName"');
    expect(identity).toContain('id="profile-lastName"');
    expect(identity).toContain("createProfileSchema");
  });

  it("shows the e-mail as text, never as a field", () => {
    // No endpoint changes it; a disabled input would only suggest one exists.
    expect(identity.match(/<Input\b/g)).toHaveLength(2);
    expect(identity).not.toMatch(/<Input[^>]*type="email"/);
    expect(identity).toContain('t("emailLocked")');
  });

  it("shows the member-since date and falls back to the e-mail's local part for the name", () => {
    expect(identity).toContain("user.createdAt");
    expect(identity).toContain('t("memberSince"');
    expect(identity).toContain("displayName(user)");
  });

  it("reads the plan from the flag-free endpoint and offers a purchase only through canSeeProNav", () => {
    expect(plan).toContain("authApi.plan");
    expect(plan).toContain("resolvePlanCardState(plan, config)");
    expect(plan).not.toContain("paymentsApi");
    expect(plan).not.toContain("jobSourcesApi");
    // The link target is the one the rest of the app uses for the paid plan.
    expect(plan).toContain("PRO_NAV_HREF");
  });

  it("summarises contributions here and manages them on the one contributions page", () => {
    expect(contributions).toContain("takeRecent(");
    // Three doors, one page: /my-salaries and /my-experiences are redirects since 2026-09-18.
    expect(contributions.match(/href="\/my-reviews"/g)).toHaveLength(3);
    expect(contributions).not.toContain('href="/my-salaries"');
    expect(contributions).not.toContain('href="/my-experiences"');
    expect(contributions).toContain('href="/contribute?tab=review"');
    expect(contributions).toContain('href="/contribute?tab=salary"');
    expect(contributions).toContain('href="/contribute?tab=experience"');
    expect(contributions).not.toContain("companyReviewsApi.remove");
    expect(contributions).not.toContain("companySalariesApi.remove");
    expect(contributions).not.toContain("candidateExperiencesApi.remove");
  });

  it("draws the contributions only behind their flags, and points at settings for the rest", () => {
    expect(page).toContain("config.companyReviews?.enabled === true");
    expect(page).toContain("config.companySalaries?.enabled === true");
    expect(page).toContain("config.candidateExperiences?.enabled === true");
    expect(page).toContain("{reviewsOn && <ContributionsCard showSalaries={salariesOn} showExperiences={experiencesOn} />}");
    expect(page).toContain('href="/settings"');
    expect(page).toContain("<ActivityTiles />");
  });

  it("names the menu entry the way the page names itself, in both languages", () => {
    expect(tr.nav.profile).toBe(tr.profile.title);
    expect(en.nav.profile).toBe(en.profile.title);
  });

  it("offers a break in the three lengths the API accepts, and only from the profile (T5)", () => {
    const card = read("components/profile/BreakCard.tsx");
    const api = read("lib/api/reminders.ts");
    const gate = read("components/dashboard/ReminderBreakGate.tsx");
    const dashboard = read("app/[locale]/(protected)/dashboard/page.tsx");

    expect(page).toContain("<BreakCard />");
    expect(api).toContain("export const BREAK_LENGTHS = [7, 14, 30] as const;");
    expect(card).toContain("BREAK_LENGTHS.map");
    for (const messages of [tr, en]) {
      expect(Object.keys(messages.profile.break.length)).toEqual(["7", "14", "30"]);
    }

    // The dashboard hides exactly the things a break is about — the two reminder surfaces and,
    // since 2026-09-24, the invitation to rate ended processes — and nothing else, and the way
    // back is the profile — the gate never starts a break itself.
    expect(dashboard).toContain(
      "<ReminderBreakGate>\n            <StaleApplicationsBanner />\n            <RemindersPanel />\n            <EndedProcessesCard />\n          </ReminderBreakGate>",
    );
    expect(gate).toContain('href="/profile"');
    expect(gate).not.toContain("useStartBreak");
  });

  it("greets the return with one question and never with the pile, in both languages", () => {
    for (const messages of [tr, en]) {
      expect(messages.dashboard.break.returnedSome).toContain("{count");
      expect(messages.dashboard.break.returnedNone).not.toContain("{count");
      expect(messages.dashboard.break.close).toContain("{count}");
    }
  });
});
