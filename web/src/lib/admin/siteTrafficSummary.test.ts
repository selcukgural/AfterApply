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

  it("adds the tools' sign-up clicks to every other sign-up button", () => {
    const summary = summariseTraffic([
      row("CtaGetStarted", "/", 8),
      row("CtaHeaderGetStarted", "/", 1),
      row("CtaHeroSocialSignIn", "/", 3),
      row("CtaToolSignUp", "/cv-tarama", 4),
      row("CtaToolSignUp", "/benchmark", 2),
    ])!;

    expect(summary.toolSignUpClicks).toBe(6);
    expect(summary.signUpClicks).toBe(18);
  });

  it("reads each free tool from its own pages, in both languages", () => {
    const summary = summariseTraffic([
      row("PageView", "/cv-tarama", 150),
      row("PageView", "/cv-scan", 17),
      row("CvScanCompleted", "/cv-tarama", 90),
      row("CvScanCompleted", "/cv-scan", 4),
      row("CtaToolSignUp", "/cv-tarama", 5),
      row("ShareClicked", "/cv-tarama", 2),
      row("PageView", "/benchmark", 111),
      row("ToolResultShown", "/benchmark", 40),
      row("PageView", "/offer-comparison", 9),
      row("ToolResultShown", "/teklif-karsilastirma", 6),
      row("PageView", "/response-rates", 30),
      row("CtaToolSignUp", "/response-rates", 1),
      row("PageView", "/companies/acme", 50),
      row("PageView", "/companies/globex", 8),
      row("PageView", "/companies", 400),
      row("ToolResultShown", "/companies/acme", 3),
      row("ShareClicked", "/companies/acme", 7),
    ])!;
    const tool = (key: string) => summary.tools.find((t) => t.key === key)!;

    expect(tool("cvScan")).toEqual({ key: "cvScan", views: 167, results: 94, signUpClicks: 5, shares: 2 });
    expect(tool("benchmark")).toMatchObject({ views: 111, results: 40, signUpClicks: 0 });
    expect(tool("offerCompare")).toMatchObject({ views: 9, results: 6, shares: null });
    // A page with nothing to run has no result count, rather than a misleading zero.
    expect(tool("responseRates")).toMatchObject({ views: 30, results: null, signUpClicks: 1 });
    // The report lives on the company pages, not on the directory itself; a company page's share
    // button is not the report's.
    expect(tool("silenceReport")).toMatchObject({ views: 58, results: 3, shares: null });
  });
});
