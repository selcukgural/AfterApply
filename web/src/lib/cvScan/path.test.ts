import { describe, expect, it } from "vitest";
import { cvScanPath, cvScanRedirectForPath, cvScanScoreCardOf, cvScanScorePath } from "./path";

describe("cvScanPath", () => {
  it("is the Turkish slug under /tr and the English one under /en", () => {
    expect(cvScanPath("tr")).toBe("/cv-tarama");
    expect(cvScanPath("en")).toBe("/cv-scan");
  });

  it("falls back to the default locale's slug", () => {
    expect(cvScanPath("de")).toBe("/cv-tarama");
  });

  it("puts the score page under each locale's own segment", () => {
    expect(cvScanScorePath("tr", "88")).toBe("/cv-tarama/puan/88");
    expect(cvScanScorePath("en", "88-28-22-20-18")).toBe("/cv-scan/score/88-28-22-20-18");
  });
});

describe("cvScanRedirectForPath", () => {
  it("sends the wrong slug to the right one, permanently, in both directions", () => {
    expect(cvScanRedirectForPath("/en/cv-tarama")).toBe("/en/cv-scan");
    expect(cvScanRedirectForPath("/tr/cv-scan")).toBe("/tr/cv-tarama");
    expect(cvScanRedirectForPath("/en/cv-tarama/")).toBe("/en/cv-scan");
  });

  it("carries a score page across, translating the segment too", () => {
    expect(cvScanRedirectForPath("/en/cv-tarama/puan/88")).toBe("/en/cv-scan/score/88");
    expect(cvScanRedirectForPath("/tr/cv-scan/score/88-28-22-20-18")).toBe("/tr/cv-tarama/puan/88-28-22-20-18");
    // Right slug, wrong segment: the same page, spelled the way this locale spells it.
    expect(cvScanRedirectForPath("/en/cv-scan/puan/88")).toBe("/en/cv-scan/score/88");
  });

  it("leaves the right addresses and everything else alone", () => {
    expect(cvScanRedirectForPath("/tr/cv-tarama")).toBeNull();
    expect(cvScanRedirectForPath("/en/cv-scan")).toBeNull();
    expect(cvScanRedirectForPath("/en/cv-scan/score/88")).toBeNull();
    expect(cvScanRedirectForPath("/tr/cv-tarama/puan/88")).toBeNull();
    expect(cvScanRedirectForPath("/tr/help/cv-scan")).toBeNull();
    expect(cvScanRedirectForPath("/cv-tarama")).toBeNull();
    expect(cvScanRedirectForPath("/tr/benchmark")).toBeNull();
  });
});

describe("cvScanScoreCardOf", () => {
  it("reads the card and the locale off a score page in either spelling", () => {
    expect(cvScanScoreCardOf("/tr/cv-tarama/puan/88")).toEqual({ locale: "tr", card: "88" });
    expect(cvScanScoreCardOf("/en/cv-scan/score/88-34-22-13-19/")).toEqual({ locale: "en", card: "88-34-22-13-19" });
    // Wrong spelling for the locale is still a score page — the redirect decides where it goes.
    expect(cvScanScoreCardOf("/en/cv-tarama/puan/101")).toEqual({ locale: "en", card: "101" });
  });

  it("is null for the tool page and everything else", () => {
    expect(cvScanScoreCardOf("/tr/cv-tarama")).toBeNull();
    expect(cvScanScoreCardOf("/tr/cv-tarama/puan")).toBeNull();
    expect(cvScanScoreCardOf("/tr/cv-tarama/puan/88/x")).toBeNull();
    expect(cvScanScoreCardOf("/tr/benchmark")).toBeNull();
  });
});
