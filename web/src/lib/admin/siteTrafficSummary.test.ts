import { describe, expect, it } from "vitest";
import type { SiteTrafficCounterResponse } from "@/types/api";
import { formatRate } from "@/lib/dashboard/format";
import { summariseTraffic } from "./siteTrafficSummary";

const row = (event: string, path: string, count: number, referrerHost = ""): SiteTrafficCounterResponse => ({
  day: "2026-10-02",
  event,
  path,
  locale: "tr",
  referrerHost,
  count,
});

describe("summariseTraffic", () => {
  it("returns null when nothing was counted", () => {
    expect(summariseTraffic(undefined)).toBeNull();
    expect(summariseTraffic([])).toBeNull();
  });

  it("counts form and social sign-ups as new accounts", () => {
    const summary = summariseTraffic([
      row("PageView", "/", 500),
      row("RegisterCompleted", "/register", 5),
      row("RegisterSocialStarted", "/register", 22),
      row("RegisterSocialCompleted", "/register", 18),
    ])!;

    expect(summary.registerCompleted).toBe(5);
    expect(summary.socialStarted).toBe(22);
    expect(summary.socialCompleted).toBe(18);
    expect(summary.newAccounts).toBe(23);
  });

  it("gives the landing ratio as a percentage, so it does not render as 0%", () => {
    const summary = summariseTraffic([row("PageView", "/", 556), row("RegisterCompleted", "/register", 5)])!;

    expect(summary.landingToRegister).toBeCloseTo((5 / 556) * 100);
    expect(formatRate(summary.landingToRegister!, "tr")).toBe("%0,9");
  });

  it("has no landing ratio without landing views", () => {
    expect(summariseTraffic([row("RegisterCompleted", "/register", 1)])!.landingToRegister).toBeNull();
  });

  it("keeps the header and in-page calls to action apart and reads the new funnel steps", () => {
    const summary = summariseTraffic([
      row("CtaGetStarted", "/", 7),
      row("CtaHeaderGetStarted", "/", 4),
      row("CtaHeaderGetStarted", "/companies", 9),
      row("PageView", "/register", 55),
      row("PageView", "/register", 3, "linkedin.com"),
      row("RegisterPasswordRejected", "/register", 2),
      row("CvScanCompleted", "/cv-tarama", 31),
      row("CtaHeroSocialSignIn", "/", 6),
    ])!;

    expect(summary.ctaClicks).toBe(7);
    expect(summary.headerCtaClicks).toBe(13);
    expect(summary.registerViews).toBe(58);
    expect(summary.passwordRejected).toBe(2);
    expect(summary.cvScans).toBe(31);
    expect(summary.heroSocialClicks).toBe(6);
  });
});
