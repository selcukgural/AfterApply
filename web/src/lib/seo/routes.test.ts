import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import robots from "@/app/robots";
import { companySitemapEntries, staticSitemapEntries } from "@/app/sitemap";
import { routing } from "@/i18n/routing";
import { GUIDE_ARTICLES, GUIDE_PATH, articlePath } from "@/lib/guide/articles";
import { HELP_TOPICS, PROTECTED_PATHS, PUBLIC_PATHS, SITE_URL, alternateLanguages, disallowedPaths, pathFor } from "./routes";

describe("disallowedPaths", () => {
  // The bug this replaces: robots.txt disallowed "/dashboard", but `localePrefix: "always"` means
  // the only URL that exists is "/tr/dashboard" — so the rule matched nothing and the signed-in
  // areas were, in robots.txt terms, wide open.
  it("prefixes every path with every locale", () => {
    expect(disallowedPaths(["tr", "en"], ["/dashboard", "/settings"])).toEqual([
      "/tr/dashboard",
      "/tr/settings",
      "/en/dashboard",
      "/en/settings",
    ]);
  });

  it("leaves no unprefixed path behind", () => {
    for (const path of disallowedPaths(routing.locales, PROTECTED_PATHS)) {
      expect(path).toMatch(/^\/(tr|en)\//);
    }
  });
});

describe("alternateLanguages", () => {
  it("names both locales and an x-default", () => {
    expect(alternateLanguages("/help/faq")).toEqual({
      tr: "/tr/help/faq",
      en: "/en/help/faq",
      "x-default": "/tr/help/faq",
    });
  });

  it("points x-default at the locale the bare / redirects to", () => {
    expect(alternateLanguages("")["x-default"]).toBe(`/${routing.defaultLocale}`);
  });

  it("absolutises against a base for the sitemap", () => {
    expect(alternateLanguages("/login", SITE_URL).tr).toBe("https://ekariyerim.com/tr/login");
  });
});

describe("robots", () => {
  it("keeps every signed-in area out, under both locales", () => {
    const { rules } = robots();
    const disallow = (Array.isArray(rules) ? rules[0] : rules).disallow as string[];

    expect(disallow).toContain("/tr/dashboard");
    expect(disallow).toContain("/en/dashboard");
    expect(disallow).toContain("/tr/applications");
    expect(disallow).toContain("/en/settings");
    expect(disallow).toHaveLength(routing.locales.length * PROTECTED_PATHS.length);
  });

  it("points at the sitemap on the canonical host", () => {
    expect(robots().sitemap).toBe("https://ekariyerim.com/sitemap.xml");
  });
});

describe("sitemap", () => {
  const entries = staticSitemapEntries();

  it("lists every public path in every locale", () => {
    expect(entries).toHaveLength(routing.locales.length * PUBLIC_PATHS.length);
    expect(entries.map((entry) => entry.url)).toContain("https://ekariyerim.com/tr/help/chrome-extension");
    expect(entries.map((entry) => entry.url)).toContain("https://ekariyerim.com/en");
  });

  it("claims no lastModified it cannot substantiate", () => {
    expect(entries.every((entry) => entry.lastModified === undefined)).toBe(true);
  });

  it("carries absolute hreflang alternates on every entry", () => {
    for (const entry of entries) {
      const languages = entry.alternates?.languages as Record<string, string>;
      expect(languages["x-default"]).toBe(languages.tr);
      for (const href of Object.values(languages)) {
        expect(href.startsWith(`${SITE_URL}/`)).toBe(true);
      }
    }
  });

  it("lists no path that robots.txt disallows", () => {
    const disallowed = new Set(disallowedPaths(routing.locales, PROTECTED_PATHS).map((path) => `${SITE_URL}${path}`));
    expect(entries.filter((entry) => disallowed.has(entry.url))).toEqual([]);
  });
});

describe("company pages in the sitemap", () => {
  const entries = companySitemapEntries([{ slug: "turk-telekom-a-s", lastApprovedAt: "2026-09-12T10:00:00Z" }]);

  it("lists each reviewed company under both locales with the approval date as lastModified", () => {
    expect(entries.map((entry) => entry.url)).toEqual([
      `${SITE_URL}/tr/companies/turk-telekom-a-s`,
      `${SITE_URL}/en/companies/turk-telekom-a-s`,
    ]);
    expect(entries.every((entry) => entry.lastModified instanceof Date)).toBe(true);
  });

  it("lists nothing when no company has a published review", () => {
    expect(companySitemapEntries([])).toEqual([]);
  });
});

describe("guide articles in the sitemap", () => {
  const entries = staticSitemapEntries();
  const urls = entries.map((entry) => entry.url);

  it("lists the index and every article under its own locale's slug", () => {
    expect(urls).toContain(`${SITE_URL}/tr${GUIDE_PATH}`);
    expect(urls).toContain(`${SITE_URL}/en${GUIDE_PATH}`);

    for (const article of GUIDE_ARTICLES) {
      expect(urls).toContain(`${SITE_URL}/tr${articlePath(article, "tr")}`);
      expect(urls).toContain(`${SITE_URL}/en${articlePath(article, "en")}`);
    }
  });

  // The translated slug is the whole point of the LocalisedPath type: an article's Turkish entry
  // has to declare the *English* slug as its `en` alternate, not its own slug under /en.
  it("pairs the two locales' different slugs as each other's alternates", () => {
    for (const article of GUIDE_ARTICLES) {
      const entry = entries.find((candidate) => candidate.url === `${SITE_URL}/tr${articlePath(article, "tr")}`);
      const languages = entry?.alternates?.languages as Record<string, string>;

      expect(languages.en).toBe(`${SITE_URL}/en${articlePath(article, "en")}`);
      expect(languages.tr).toBe(`${SITE_URL}/tr${articlePath(article, "tr")}`);
      expect(languages["x-default"]).toBe(languages.tr);
    }
  });

  it("never serves a Turkish slug under /en", () => {
    for (const article of GUIDE_ARTICLES) {
      expect(urls).not.toContain(`${SITE_URL}/en${articlePath(article, "tr")}`);
    }
  });
});

describe("pathFor", () => {
  it("passes a shared path through unchanged", () => {
    expect(pathFor("/help/faq", "en")).toBe("/help/faq");
  });

  it("picks the locale's own path when the slug is translated", () => {
    expect(pathFor({ tr: "/guide/tr-slug", en: "/guide/en-slug" }, "en")).toBe("/guide/en-slug");
  });

  it("falls back to the default locale rather than producing undefined in the URL", () => {
    expect(pathFor({ tr: "/guide/tr-slug" }, "en")).toBe("/guide/tr-slug");
  });
});

describe("help topics", () => {
  it("are all in the sitemap", () => {
    for (const topic of HELP_TOPICS) {
      expect(PUBLIC_PATHS).toContain(topic.href);
    }
  });
});

// The visit counter's allowlist lives in the API (SiteTrafficNormalizer) because that is where it
// has to be enforced — a client-side copy would be advisory. But it is a hand-written list of this
// app's routes, so it drifts the moment a page is added here and not there, and the failure is
// silent in the worst way: the new page simply never appears in any number, and nobody notices
// until they wonder why the guide looks unread. This is the tripwire for that.
const REPO_ROOT = fileURLToPath(new URL("../../../..", import.meta.url));
const NORMALIZER = readFileSync(
  path.join(REPO_ROOT, "src/AfterApply.Application/SiteTraffic/SiteTrafficNormalizer.cs"),
  "utf8",
);

/** Pulls the quoted strings out of one `X = new(...) { ... }` initialiser in the C# source. */
function csharpStringSet(field: string): string[] {
  const block = new RegExp(`${field}\\s*=[\\s\\S]*?\\{([\\s\\S]*?)\\}`).exec(NORMALIZER);
  expect(block, `${field} not found in SiteTrafficNormalizer.cs`).not.toBeNull();
  return [...block![1].matchAll(/"([^"]*)"/g)].map((match) => match[1]);
}

const EXACT_PATHS = csharpStringSet("ExactPaths");
const SLUG_SECTIONS = csharpStringSet("SlugSections");
const SLUG = /^[a-z0-9][a-z0-9-]{0,63}$/;

/** The same decision the API makes, mirrored here so the two lists can be compared. */
function isCountable(routePath: string): boolean {
  const candidate = routePath === "" ? "/" : routePath;
  if (EXACT_PATHS.includes(candidate)) return true;

  const segments = candidate.split("/").filter(Boolean);
  return segments.length === 2 && SLUG_SECTIONS.includes(`/${segments[0]}`) && SLUG.test(segments[1]);
}

describe("visit counter allowlist", () => {
  it("covers every page the sitemap publishes, in both locales", () => {
    const uncovered = PUBLIC_PATHS.flatMap((route) =>
      routing.locales.map((locale) => pathFor(route, locale)).filter((resolved) => !isCountable(resolved)),
    );

    expect(uncovered, "these pages exist but the API would never count a visit to them").toEqual([]);
  });

  it("counts no signed-in page", () => {
    // The privacy half. A signed-in path carries record ids, so one slipping into the allowlist
    // would put "someone opened application <id>" into a table that is meant to hold no such thing.
    for (const route of PROTECTED_PATHS) {
      expect(isCountable(route), `${route} must never be countable`).toBe(false);
      expect(isCountable(`${route}/0192e5c1-6f3a-7c2b-9a11-4f0d2e8b5a77`)).toBe(false);
    }
  });

  it("counts no OAuth callback, where the URL carries an authorization code", () => {
    expect(isCountable("/auth/google/callback")).toBe(false);
    expect(isCountable("/auth/linkedin/callback")).toBe(false);
  });
});

// The landing page deliberately says two different things in two places: the <title> carries the
// term people type into a search engine, the page's own copy carries the promise. The 2026-09-08
// rewrite changed the second and kept the first, and that is exactly the pairing a later copy pass
// would collapse by "cleaning up" the title — undoing the SEO work of the day before, silently,
// because nothing renders a title where a reviewer would notice it missing.
describe("landing page title", () => {
  it("still carries the term people actually search for", () => {
    expect(tr.metadata.pages.home.title.toLocaleLowerCase("tr")).toContain("başvuru takip");
    expect(en.metadata.pages.home.title.toLowerCase()).toContain("application tracker");
  });

  it("keeps that term in the description too, alongside the promise", () => {
    expect(tr.metadata.pages.home.description.toLocaleLowerCase("tr")).toContain("başvuru takib");
    expect(en.metadata.pages.home.description.toLowerCase()).toContain("application tracking");
  });
});
