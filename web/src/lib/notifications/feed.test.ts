import { describe, expect, it } from "vitest";
import type { ContributionNotificationResponse, NotificationFeedItemResponse } from "@/types/api";
import { contributionTarget, emailTarget, formatNotificationTime } from "./feed";

const contribution = (overrides: Partial<ContributionNotificationResponse>): ContributionNotificationResponse => ({
  type: "ReviewHelpful",
  targetId: "t1",
  count: 1,
  companyName: "Trendyol",
  companySlug: "trendyol",
  blogPostTitle: null,
  blogPostSlug: null,
  blogPostLanguage: null,
  ...overrides,
});

describe("contributionTarget", () => {
  it("sends each company kind to its own tab on the company page", () => {
    expect(contributionTarget(contribution({ type: "ReviewHelpful" }))).toEqual({ href: "/companies/trendyol?tab=reviews" });
    expect(contributionTarget(contribution({ type: "SalaryHelpful" }))).toEqual({ href: "/companies/trendyol?tab=salaries" });
    expect(contributionTarget(contribution({ type: "ExperienceHelpful" }))).toEqual({ href: "/companies/trendyol?tab=experiences" });
  });

  it("sends a comment to the post's comments, in the post's own language", () => {
    const c = contribution({ type: "BlogCommentHelpful", companyName: null, companySlug: null, blogPostSlug: "ghosting", blogPostLanguage: "tr" });
    expect(contributionTarget(c)).toEqual({ href: "/blog/ghosting#comments", locale: "tr" });
  });

  it("leads nowhere rather than to a broken URL when the slug is missing", () => {
    expect(contributionTarget(contribution({ companySlug: null }))).toBeNull();
    expect(contributionTarget(contribution({ type: "BlogCommentHelpful", blogPostSlug: null }))).toBeNull();
  });
});

describe("emailTarget", () => {
  const item = (applicationId: string | null): NotificationFeedItemResponse => ({
    id: "n1",
    kind: "Email",
    occurredAt: "2026-09-23T11:32:00Z",
    isRead: false,
    contribution: null,
    email: {
      id: "n1",
      applicationId,
      companyName: "Hepsiburada",
      jobTitle: "Backend Developer",
      status: "Interview",
      wasAutoApplied: true,
      isNewApplicationSuggestion: false,
      matchType: "DomainMatch",
      confidenceScore: 0.9,
      isRead: false,
      createdAt: "2026-09-23T11:32:00Z",
      resolvedAt: null,
    },
  });

  it("opens the application a Gmail row changed", () => {
    expect(emailTarget(item("app-1"))).toBe("/applications/app-1");
  });

  it("has nowhere to go without an application", () => {
    expect(emailTarget(item(null))).toBeNull();
  });
});

describe("formatNotificationTime", () => {
  it("carries both the date and the time", () => {
    const text = formatNotificationTime("2026-09-23T11:32:00Z", "en");
    expect(text).toMatch(/2026/);
    expect(text).toMatch(/September/);
    expect(text).toMatch(/\d{1,2}:\d{2}/);
  });

  it("speaks the reader's language", () => {
    expect(formatNotificationTime("2026-09-23T11:32:00Z", "tr")).toMatch(/Eylül/);
  });
});
