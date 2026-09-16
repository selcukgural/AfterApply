import { existsSync, readFileSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { join, relative } from "node:path";
import { describe, expect, it } from "vitest";
import {
  GUIDE_ARTICLES,
  GUIDE_LOCALES,
  GUIDE_PATH,
  articlePath,
  articlePaths,
  findArticleByKey,
  findArticleBySlug,
  guidePath,
  guideRedirectForPath,
  isGuideLocale,
  resolveGuideSlug,
} from "./articles";
import { GUIDE_LOADER_KEYS } from "./content";
import { HELP_TOPICS } from "@/lib/seo/routes";
import { formatArticleDate } from "./formatArticleDate";

const CONTENT_DIR = fileURLToPath(new URL("../../content/guide", import.meta.url));

describe("guide registry", () => {
  it("has a body file for every article in every locale", () => {
    const missing = GUIDE_ARTICLES.flatMap((article) =>
      GUIDE_LOCALES.map((locale) => `${article.key}.${locale}.mdx`).filter(
        (file) => !existsSync(join(CONTENT_DIR, file)),
      ),
    );

    expect(missing).toEqual([]);
  });

  // content.ts writes its import paths out one by one; nothing but this holds it to the registry.
  it("has a loader for every article and no loader without one", () => {
    expect([...GUIDE_LOADER_KEYS].sort()).toEqual(GUIDE_ARTICLES.map((article) => article.key).sort());
  });

  // remark-gfm does not take effect on this toolchain (see the comment in next.config.ts), and it
  // fails silently: a pipe table renders as a paragraph of literal pipes on the live page and
  // nothing warns you. This is the warning.
  it("uses no GFM table syntax, which the MDX pipeline does not support", () => {
    const offenders = GUIDE_ARTICLES.flatMap((article) =>
      GUIDE_LOCALES.flatMap((locale) => {
        const file = `${article.key}.${locale}.mdx`;
        const path = join(CONTENT_DIR, file);
        if (!existsSync(path)) return [];
        const lines = readFileSync(path, "utf8").split("\n");
        return lines
          .map((line, index) => ({ line, number: index + 1 }))
          .filter(({ line }) => /^\s*\|/.test(line) || /^\s*\|?\s*:?-{3,}:?\s*\|/.test(line))
          .map(({ number }) => `${file}:${number}`);
      }),
    );

    expect(offenders).toEqual([]);
  });

  it("gives every article a unique key", () => {
    const keys = GUIDE_ARTICLES.map((article) => article.key);
    expect(new Set(keys).size).toBe(keys.length);
  });

  // Two articles sharing a slug in one locale would make one of them unreachable: findArticleBySlug
  // returns the first match and the second would 404 with no other sign of a problem.
  it("gives every article a unique slug within each locale", () => {
    for (const locale of GUIDE_LOCALES) {
      const slugs = GUIDE_ARTICLES.map((article) => article.copy[locale].slug);
      expect(new Set(slugs).size).toBe(slugs.length);
    }
  });

  it("uses url-safe, lowercase slugs", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        expect(article.copy[locale].slug).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
      }
    }
  });

  it("fills in title and description for both locales", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        expect(article.copy[locale].title.trim().length).toBeGreaterThan(0);
        expect(article.copy[locale].description.trim().length).toBeGreaterThan(0);
      }
    }
  });

  it("dates every article as a plain ISO day", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const date of [article.published, article.updated].filter((value) => value !== undefined)) {
        expect(date).toMatch(/^\d{4}-\d{2}-\d{2}$/);
        expect(Number.isNaN(Date.parse(date))).toBe(false);
      }
    }
  });

  it("points every related link at an article that exists, and never at itself", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const key of article.related) {
        expect(key).not.toBe(article.key);
        expect(findArticleByKey(key)).toBeDefined();
      }
    }
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

  // guidePath throws at render time for a key that is not in the registry — this finds the same
  // mistake at test time, before a company page or the review form renders an error instead.
  it("is only asked for articles that exist, everywhere the product links into the guide", () => {
    const asked = sourceFiles(SRC).flatMap((file) =>
      [...readFileSync(file, "utf8").matchAll(/guidePath\(\s*"([^"]+)"/g)].map((match) => `${relative(SRC, file)}: ${match[1]}`),
    );
    expect(asked.length).toBeGreaterThan(0);
    expect(asked.filter((entry) => !findArticleByKey(entry.split(": ")[1]))).toEqual([]);
  });

  // The review-writing guide is reached from the review form and from a rejected review, both
  // behind login; the "start for free" box would be asking a signed-in reader to register.
  it("hides the register box on the guide that only signed-in readers reach", () => {
    expect(findArticleByKey("writing-a-fair-review")?.hideRegisterCta).toBe(true);
    expect(findArticleByKey("reading-employee-reviews")?.hideRegisterCta).toBeUndefined();
  });
});

describe("guide article bodies", () => {
  const bodies = GUIDE_ARTICLES.flatMap((article) =>
    GUIDE_LOCALES.map((locale) => ({
      article,
      locale,
      file: `${article.key}.${locale}.mdx`,
      source: readFileSync(join(CONTENT_DIR, `${article.key}.${locale}.mdx`), "utf8"),
    })),
  );

  /** Every `[text](/path)` in the prose. External links carry a scheme and are not matched. */
  function internalLinks(source: string): string[] {
    return [...source.matchAll(/\]\((\/[^)\s]+)\)/g)].map((match) => match[1]);
  }

  // A mistyped slug inside an article produces a 404 that nothing else notices: the build succeeds,
  // the page renders, and only a reader clicking the link finds out.
  it("links only to guide articles that exist in the same locale", () => {
    const broken = bodies.flatMap(({ locale, file, source }) =>
      internalLinks(source)
        .filter((href) => href.startsWith("/guide/") && !href.includes("."))
        .filter((href) => !GUIDE_ARTICLES.some((entry) => `/guide/${entry.copy[locale].slug}` === href))
        .map((href) => `${file}: ${href}`),
    );

    expect(broken).toEqual([]);
  });

  it("links only to help pages that exist", () => {
    const hrefs = new Set<string>(HELP_TOPICS.map((topic) => topic.href));
    const broken = bodies.flatMap(({ file, source }) =>
      internalLinks(source)
        .filter((href) => href.startsWith("/help"))
        .filter((href) => !hrefs.has(href))
        .map((href) => `${file}: ${href}`),
    );

    expect(broken).toEqual([]);
  });

  // The spreadsheet articles hand out files from public/; a renamed asset would leave a dead
  // download button behind, which is the one link on the page people actually came for.
  it("links only to downloadable files that are actually in public/", () => {
    const publicDir = fileURLToPath(new URL("../../../public", import.meta.url));
    const missing = bodies.flatMap(({ file, source }) =>
      internalLinks(source)
        .filter((href) => /\.[a-z0-9]{2,5}$/i.test(href))
        .filter((href) => !existsSync(join(publicDir, href)))
        .map((href) => `${file}: ${href}`),
    );

    expect(missing).toEqual([]);
  });

  // A cluster only works if the pieces point at each other; an article nothing links out of is a
  // dead end for a reader and for the crawler.
  it("gives every article at least one link to another guide article", () => {
    const orphans = bodies
      .filter(({ source }) => !internalLinks(source).some((href) => href.startsWith("/guide/") && !href.includes(".")))
      .map(({ file }) => file);

    expect(orphans).toEqual([]);
  });

  it("has real prose in every body, not a leftover placeholder", () => {
    for (const { file, source } of bodies) {
      expect(source.trim().length, file).toBeGreaterThan(1500);
      expect(source, file).not.toContain("Placeholder");
    }
  });
});

describe("guide article metadata fits a search result", () => {
  // Google truncates around 60 characters, and the rendered <title> appends " · e-kariyerim" on
  // top of this — so the bound is on the bare title and deliberately has room left over.
  it("keeps every title short enough to survive the SERP", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        const { title } = article.copy[locale];
        expect(title.length, `${article.key}.${locale}: ${title}`).toBeLessThanOrEqual(60);
        expect(title.length, `${article.key}.${locale}`).toBeGreaterThan(20);
      }
    }
  });

  it("keeps every description inside the length a result snippet shows", () => {
    for (const article of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        const { description } = article.copy[locale];
        expect(description.length, `${article.key}.${locale}: ${description}`).toBeLessThanOrEqual(160);
        expect(description.length, `${article.key}.${locale}`).toBeGreaterThanOrEqual(110);
      }
    }
  });
});

describe("guide lookups", () => {
  it("resolves a slug back to its article within the right locale only", () => {
    const [article] = GUIDE_ARTICLES;

    expect(findArticleBySlug(article.copy.tr.slug, "tr")).toBe(article);
    // The Turkish slug must not resolve under /en — that would serve one page on two URLs.
    expect(findArticleBySlug(article.copy.tr.slug, "en")).toBeUndefined();
  });

  it("returns undefined for an unknown slug rather than throwing", () => {
    expect(findArticleBySlug("no-such-article", "tr")).toBeUndefined();
  });

  it("builds a per-locale path set for the hreflang pairs", () => {
    const [article] = GUIDE_ARTICLES;

    expect(articlePaths(article)).toEqual({
      tr: `${GUIDE_PATH}/${article.copy.tr.slug}`,
      en: `${GUIDE_PATH}/${article.copy.en.slug}`,
    });
    expect(articlePath(article, "en")).toBe(`${GUIDE_PATH}/${article.copy.en.slug}`);
  });

  it("recognises only the locales the guide is written in", () => {
    expect(isGuideLocale("tr")).toBe(true);
    expect(isGuideLocale("de")).toBe(false);
  });
});

describe("a slug in the wrong locale still finds its article", () => {
  const article = findArticleByKey("linkedin-application-history")!;

  it("resolves the locale's own slug without a redirect", () => {
    expect(resolveGuideSlug(article.copy.tr.slug, "tr")).toEqual({ article });
    expect(resolveGuideSlug(article.copy.en.slug, "en")).toEqual({ article });
  });

  it("sends the other locale's slug to the requested locale's own address", () => {
    // /tr/guide/export-linkedin-application-history → the Turkish article at its Turkish slug.
    expect(resolveGuideSlug(article.copy.en.slug, "tr")).toEqual({
      article,
      redirectTo: `${GUIDE_PATH}/${article.copy.tr.slug}`,
    });
    expect(resolveGuideSlug(article.copy.tr.slug, "en")).toEqual({
      article,
      redirectTo: `${GUIDE_PATH}/${article.copy.en.slug}`,
    });
  });

  it("knows nothing about a slug no locale has", () => {
    expect(resolveGuideSlug("no-such-article", "tr")).toBeUndefined();
  });

  it("never redirects an article to the address it is already at", () => {
    for (const entry of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        expect(resolveGuideSlug(entry.copy[locale].slug, locale)?.redirectTo).toBeUndefined();
      }
    }
  });
});

describe("the proxy's guide redirects", () => {
  const article = findArticleByKey("kariyer-net-application-history")!;

  // Search Console: /tr/guide/export-linkedin-application-history and the reverse.
  it("sends the other locale's slug under a prefix to that locale's own slug", () => {
    expect(guideRedirectForPath(`/tr/guide/${article.copy.en.slug}`)).toBe(
      `/tr${GUIDE_PATH}/${article.copy.tr.slug}`,
    );
    expect(guideRedirectForPath(`/en/guide/${article.copy.tr.slug}`)).toBe(
      `/en${GUIDE_PATH}/${article.copy.en.slug}`,
    );
    expect(guideRedirectForPath(`/en/guide/${article.copy.tr.slug}/`)).toBe(
      `/en${GUIDE_PATH}/${article.copy.en.slug}`,
    );
  });

  it("prefixes a locale-less Turkish slug with /tr and an English one with /en", () => {
    expect(guideRedirectForPath(`/guide/${article.copy.tr.slug}`)).toBe(`/tr${GUIDE_PATH}/${article.copy.tr.slug}`);
    expect(guideRedirectForPath(`/guide/${article.copy.en.slug}`)).toBe(`/en${GUIDE_PATH}/${article.copy.en.slug}`);
    expect(guideRedirectForPath(`/guide/${article.copy.en.slug}/`)).toBe(`/en${GUIDE_PATH}/${article.copy.en.slug}`);
  });

  it("leaves a correct address, an unknown slug and everything else alone", () => {
    for (const entry of GUIDE_ARTICLES) {
      for (const locale of GUIDE_LOCALES) {
        expect(guideRedirectForPath(`/${locale}${GUIDE_PATH}/${entry.copy[locale].slug}`)).toBeNull();
      }
    }
    expect(guideRedirectForPath("/guide")).toBeNull();
    expect(guideRedirectForPath("/tr/guide")).toBeNull();
    expect(guideRedirectForPath("/guide/no-such-article")).toBeNull();
    expect(guideRedirectForPath("/tr/guide/no-such-article")).toBeNull();
    expect(guideRedirectForPath("/de/guide/no-such-article")).toBeNull();
    expect(guideRedirectForPath(`/guide/${article.copy.en.slug}/extra`)).toBeNull();
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
