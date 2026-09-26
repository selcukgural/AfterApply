import { readFileSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import { GUIDE_LINKS, GUIDE_LOCALES, GUIDE_PATH, guidePath, guideRedirectForPath, isGuideLocale } from "./guideLinks";
import { formatArticleDate } from "./formatArticleDate";

describe("the guide link table", () => {
  it("gives every guide a url-safe, lowercase slug in both locales, unique within each", () => {
    for (const locale of GUIDE_LOCALES) {
      const slugs = Object.values(GUIDE_LINKS).map((slugs) => slugs[locale]);
      expect(new Set(slugs).size).toBe(slugs.length);
      for (const slug of slugs) expect(slug).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
    }
  });

  it("recognises only the locales the guide is written in", () => {
    expect(isGuideLocale("tr")).toBe(true);
    expect(isGuideLocale("en")).toBe(true);
    expect(isGuideLocale("de")).toBe(false);
  });
});

describe("guide links from the product", () => {
  const SRC = fileURLToPath(new URL("../..", import.meta.url));

  function sourceFiles(dir: string): string[] {
    return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
      const full = join(dir, entry.name);
      if (entry.isDirectory()) return sourceFiles(full);
      return /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name) ? [full] : [];
    });
  }

  it("resolves a key to the locale's own slug", () => {
    expect(guidePath("reading-employee-reviews", "tr")).toBe("/guide/calisan-deneyimlerini-nasil-okumali");
    expect(guidePath("reading-employee-reviews", "en")).toBe("/guide/how-to-read-employee-reviews");
  });

  it("falls back to the default locale for one the guide does not have", () => {
    expect(guidePath("reading-employee-reviews", "de")).toBe(guidePath("reading-employee-reviews", "tr"));
  });

  it("refuses a key nobody registered", () => {
    expect(() => guidePath("no-such-article", "tr")).toThrow(/no-such-article/);
  });

  // guidePath throws at render time for a key that is not in the table — this finds the same
  // mistake at test time, before a company page or the review form renders an error instead.
  it("is only asked for guides in the table, everywhere the product links into the guide", () => {
    const asked = sourceFiles(SRC).flatMap((file) =>
      [...readFileSync(file, "utf8").matchAll(/guidePath\(\s*"([^"]+)"/g)].map((match) => `${relative(SRC, file)}: ${match[1]}`),
    );
    expect(asked.length).toBeGreaterThan(0);
    expect(asked.filter((entry) => !(entry.split(": ")[1] in GUIDE_LINKS))).toEqual([]);
  });
});

describe("the proxy's guide redirects", () => {
  const slugs = GUIDE_LINKS["kariyer-net-application-history"];

  // Search Console: /tr/guide/export-linkedin-application-history and the reverse.
  it("sends the other locale's slug under a prefix to that locale's own slug", () => {
    expect(guideRedirectForPath(`/tr/guide/${slugs.en}`)).toBe(`/tr${GUIDE_PATH}/${slugs.tr}`);
    expect(guideRedirectForPath(`/en/guide/${slugs.tr}`)).toBe(`/en${GUIDE_PATH}/${slugs.en}`);
    expect(guideRedirectForPath(`/en/guide/${slugs.tr}/`)).toBe(`/en${GUIDE_PATH}/${slugs.en}`);
  });

  it("prefixes a locale-less Turkish slug with /tr and an English one with /en", () => {
    expect(guideRedirectForPath(`/guide/${slugs.tr}`)).toBe(`/tr${GUIDE_PATH}/${slugs.tr}`);
    expect(guideRedirectForPath(`/guide/${slugs.en}`)).toBe(`/en${GUIDE_PATH}/${slugs.en}`);
    expect(guideRedirectForPath(`/guide/${slugs.en}/`)).toBe(`/en${GUIDE_PATH}/${slugs.en}`);
  });

  it("does not redirect a slug both locales share to anywhere but itself", () => {
    // "kariyer-net-application-history" is the English slug; "reapplying-to-the-same-company"
    // happens to be English only too — a correct address is never sent elsewhere.
    for (const entry of Object.values(GUIDE_LINKS)) {
      for (const locale of GUIDE_LOCALES) {
        expect(guideRedirectForPath(`/${locale}${GUIDE_PATH}/${entry[locale]}`)).toBeNull();
      }
    }
  });

  it("leaves the index, the preview, an unknown slug and everything else alone", () => {
    expect(guideRedirectForPath("/guide")).toBeNull();
    expect(guideRedirectForPath("/tr/guide")).toBeNull();
    expect(guideRedirectForPath("/tr/guide/preview/0199a0a0-0000-7000-8000-000000000001")).toBeNull();
    // A guide written in the editor after the move is not in the table: the page itself answers.
    expect(guideRedirectForPath("/guide/a-new-guide")).toBeNull();
    expect(guideRedirectForPath("/tr/guide/a-new-guide")).toBeNull();
    expect(guideRedirectForPath("/de/guide/no-such-article")).toBeNull();
    expect(guideRedirectForPath(`/guide/${slugs.en}/extra`)).toBeNull();
    expect(guideRedirectForPath("/help/faq")).toBeNull();
  });
});

describe("formatArticleDate", () => {
  // A plain "2026-09-08" parsed in a negative-offset zone would otherwise print the 7th. The
  // English form is US ordering because that is what a bare "en" resolves to, and every other date
  // in the app (ApplicationTable, CvManager, dashboard/format.ts) hands Intl the same bare locale.
  it("prints the calendar day it was given, whatever the server's time zone", () => {
    expect(formatArticleDate("2026-09-08", "tr")).toBe("8 Eylül 2026");
    expect(formatArticleDate("2026-09-08", "en")).toBe("September 8, 2026");
  });
});
