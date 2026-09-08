import { describe, expect, it } from "vitest";
import robots from "@/app/robots";
import sitemap from "@/app/sitemap";
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
  const entries = sitemap();

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

describe("guide articles in the sitemap", () => {
  const entries = sitemap();
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
