import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import { buildNavEntries, isNavItemActive, type NavEntry } from "./navGroups";

const ALL_ON = {
  jobSources: { enabled: true },
  companyReviews: { enabled: true, maxReviewsPerUser: 10, minimumReviewsForScore: 3, priorWeight: 5 },
  companySalaries: { enabled: true, maxEntriesPerUser: 10, minimumEntriesForStats: 3 },
};

const hrefs = (entries: NavEntry[]) => entries.flatMap((entry) => (entry.type === "link" ? [entry.href] : entry.items.map((item) => item.href)));
const group = (entries: NavEntry[], key: string) => {
  const entry = entries.find((candidate) => candidate.type === "group" && candidate.key === key);
  if (!entry || entry.type !== "group") throw new Error(`no group ${key}`);
  return entry;
};

describe("buildNavEntries", () => {
  it("orders the row: dashboard, applications, CVs, companies, tools", () => {
    expect(buildNavEntries(ALL_ON).map((entry) => (entry.type === "link" ? entry.href : entry.key))).toEqual([
      "/dashboard",
      "applicationsMenu",
      "/cv",
      "companies",
      "tools",
    ]);
  });

  it("groups by object: everything about companies is one group, browse then contribute then mine", () => {
    // 2026-09-17: the contribution forms used to sit in "Explore" and the author's own lists in
    // the avatar menu — the same thing, two menus.
    const companies = group(buildNavEntries(ALL_ON), "companies");
    expect(companies.items.map((item) => item.href)).toEqual([
      "/companies",
      "/contribute?tab=review",
      "/contribute?tab=salary",
      "/my-reviews",
      "/my-salaries",
    ]);
    expect(companies.items.filter((item) => item.dividerBefore).map((item) => item.href)).toEqual(["/contribute?tab=review", "/my-reviews"]);
  });

  it("keeps the account-free tools together, with the paid postings first when they exist", () => {
    expect(group(buildNavEntries(ALL_ON), "tools").items.map((item) => item.href)).toEqual(["/weekly-jobs", "/cv-tarama", "/benchmark", "/guide"]);
    expect(group(buildNavEntries({}), "tools").items.map((item) => item.href)).toEqual(["/cv-tarama", "/benchmark", "/guide"]);
    expect(group(buildNavEntries(ALL_ON), "tools").items[0].proBadge).toBe(true);
  });

  it("does not list 'new application' — that is the header's primary button, on every page", () => {
    expect(hrefs(buildNavEntries(ALL_ON))).not.toContain("/applications/new");
  });

  it("follows the company flags: no reviews, no group; no salaries, no salary items", () => {
    // The menu used to link to /contribute?tab=salary and /my-salaries unconditionally while
    // the contribute page itself hid its salary side behind the flag.
    expect(buildNavEntries({}).some((entry) => entry.type === "group" && entry.key === "companies")).toBe(false);
    const reviewsOnly = buildNavEntries({ ...ALL_ON, companySalaries: { ...ALL_ON.companySalaries, enabled: false } });
    expect(group(reviewsOnly, "companies").items.map((item) => item.href)).toEqual(["/companies", "/contribute?tab=review", "/my-reviews"]);
  });

  it("uses only keys both catalogues have", () => {
    const keys = buildNavEntries(ALL_ON).flatMap((entry) => (entry.type === "link" ? [entry.key] : [entry.key, ...entry.items.map((item) => item.key)]));
    for (const key of keys) {
      expect(tr.nav, `tr nav.${key}`).toHaveProperty(key);
      expect(en.nav, `en nav.${key}`).toHaveProperty(key);
    }
  });

  it("names actions as verbs and pages as nouns", () => {
    // "Maaş Bilgisi" promised a page and opened a form; a form is named for what you do on it.
    expect(tr.nav.writeReview).toMatch(/yaz$/);
    expect(tr.nav.shareSalary).toMatch(/paylaş$/);
    expect(en.nav.writeReview).toMatch(/^Write/);
    expect(en.nav.shareSalary).toMatch(/^Share/);
  });
});

describe("isNavItemActive", () => {
  it("matches a page and its children, never a query-string twin", () => {
    expect(isNavItemActive("/companies/acme", "/companies")).toBe(true);
    expect(isNavItemActive("/contribute", "/contribute?tab=review")).toBe(false);
    expect(isNavItemActive("/companies-x", "/companies")).toBe(false);
  });
});

describe("one name per destination", () => {
  // B7 on the 2026-09-17 navigation canvas: /companies had three names, /benchmark three.
  it("calls the companies index and the benchmark the same thing in every menu", () => {
    for (const m of [tr, en]) {
      expect(m.siteNav.companies).toBe(m.nav.companies);
      expect(m.landing.footer.companies).toBe(m.nav.companies);
      expect(m.notFound.companies).toBe(m.nav.companies);
      expect(m.siteNav.benchmark).toBe(m.nav.benchmark);
      expect(m.landing.footer.benchmark).toBe(m.nav.benchmark);
      expect(m.siteNav.guide).toBe(m.nav.guide);
      expect(m.landing.footer.guide).toBe(m.nav.guide);
      expect(m.landing.footer.cvScan).toBe(m.nav.cvScan);
    }
  });

  it("no longer sells 'free tools' to someone who is already signed in", () => {
    expect(tr.nav.tools).not.toMatch(/ücretsiz/i);
    expect(en.nav.tools).not.toMatch(/free/i);
  });
});
