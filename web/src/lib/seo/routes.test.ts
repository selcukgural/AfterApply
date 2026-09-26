import { existsSync, readFileSync, readdirSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import en from "../../../messages/en.json";
import tr from "../../../messages/tr.json";
import robots from "@/app/robots";
import { blogSitemapEntries, companySitemapEntries, guideSitemapEntries, staticSitemapEntries } from "@/app/sitemap";
import { routing } from "@/i18n/routing";
import { GUIDE_PATH } from "@/lib/guide/guideLinks";
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

  // /weekly-jobs shipped without a line here and sat crawlable (found 2026-09-25): the list is
  // read against the (protected) route group itself, so a new signed-in area cannot be missed.
  it("covers every signed-in area the app actually has", () => {
    const protectedDir = fileURLToPath(new URL("../../app/[locale]/(protected)", import.meta.url));
    const areas = readdirSync(protectedDir, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
      .map((entry) => `/${entry.name}`);
    expect(areas.length).toBeGreaterThan(0);
    expect([...PROTECTED_PATHS].sort()).toEqual(areas.sort());
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

describe("blog posts in the sitemap", () => {
  it("lists nothing while nothing is published — the index is a 404 then", () => {
    expect(blogSitemapEntries([])).toEqual([]);
    expect(PUBLIC_PATHS).not.toContain("/blog");
  });

  it("lists an index per language that has a post, and each post under its own language only", () => {
    const entries = blogSitemapEntries([
      { language: "tr", slug: "ise-alim", publishedAt: "2026-09-19T10:00:00Z", updatedAt: "2026-09-20T10:00:00Z", translation: null },
    ]);
    expect(entries.map((entry) => entry.url)).toEqual([`${SITE_URL}/tr/blog`, `${SITE_URL}/tr/blog/ise-alim`]);
    expect(entries[1].lastModified).toEqual(new Date("2026-09-20T10:00:00Z"));
    expect(entries[1].alternates?.languages).toEqual({
      tr: `${SITE_URL}/tr/blog/ise-alim`,
      "x-default": `${SITE_URL}/tr/blog/ise-alim`,
    });
    expect(entries[0].alternates?.languages).not.toHaveProperty("en");
  });

  it("links a translated pair both ways", () => {
    const entries = blogSitemapEntries([
      { language: "tr", slug: "merhaba", publishedAt: "2026-09-19T10:00:00Z", updatedAt: "2026-09-19T10:00:00Z", translation: { language: "en", slug: "hello" } },
      { language: "en", slug: "hello", publishedAt: "2026-09-19T10:00:00Z", updatedAt: "2026-09-19T10:00:00Z", translation: { language: "tr", slug: "merhaba" } },
    ]);
    expect(entries.map((entry) => entry.url)).toEqual([
      `${SITE_URL}/tr/blog`,
      `${SITE_URL}/en/blog`,
      `${SITE_URL}/tr/blog/merhaba`,
      `${SITE_URL}/en/blog/hello`,
    ]);
    expect(entries[3].alternates?.languages).toEqual({
      en: `${SITE_URL}/en/blog/hello`,
      tr: `${SITE_URL}/tr/blog/merhaba`,
      "x-default": `${SITE_URL}/tr/blog/merhaba`,
    });
  });
});

describe("guide articles in the sitemap (2026-09-26: from the database)", () => {
  const guides = [
    {
      language: "tr" as const,
      slug: "basvurularim-nereye-gitti",
      publishedAt: "2026-09-23T09:00:00Z",
      updatedAt: "2026-09-23T09:00:00Z",
      translation: { language: "en" as const, slug: "where-did-my-applications-go" },
    },
    {
      language: "en" as const,
      slug: "where-did-my-applications-go",
      publishedAt: "2026-09-23T09:00:00Z",
      updatedAt: "2026-09-23T09:00:00Z",
      translation: { language: "tr" as const, slug: "basvurularim-nereye-gitti" },
    },
  ];

  it("keeps the index among the static pages, in both locales", () => {
    const urls = staticSitemapEntries().map((entry) => entry.url);
    expect(urls).toContain(`${SITE_URL}/tr${GUIDE_PATH}`);
    expect(urls).toContain(`${SITE_URL}/en${GUIDE_PATH}`);
    expect(urls.filter((url) => url.includes(`${GUIDE_PATH}/`))).toEqual([]);
  });

  it("lists every guide under its own language with the last publish as lastModified", () => {
    const entries = guideSitemapEntries(guides);
    expect(entries.map((entry) => entry.url)).toEqual([
      `${SITE_URL}/tr/guide/basvurularim-nereye-gitti`,
      `${SITE_URL}/en/guide/where-did-my-applications-go`,
    ]);
    expect(entries[0].lastModified).toEqual(new Date("2026-09-23T09:00:00Z"));
  });

  // The translated slug is the whole point: the Turkish entry names the *English* slug as its
  // `en` alternate, not its own slug under /en.
  it("pairs the two languages' different slugs as each other's alternates", () => {
    const languages = guideSitemapEntries(guides)[0].alternates?.languages as Record<string, string>;
    expect(languages).toEqual({
      tr: `${SITE_URL}/tr/guide/basvurularim-nereye-gitti`,
      en: `${SITE_URL}/en/guide/where-did-my-applications-go`,
      "x-default": `${SITE_URL}/tr/guide/basvurularim-nereye-gitti`,
    });
  });

  it("is empty when the API is not there, so the sitemap degrades rather than fails", () => {
    expect(guideSitemapEntries([])).toEqual([]);
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

describe("the Pro plan's sale terms", () => {
  it("are public, indexed pages since their text went final (2026-09-16)", () => {
    expect(PUBLIC_PATHS).toContain("/terms-of-sale");
    expect(PUBLIC_PATHS).toContain("/refund-policy");
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
// Two pages with one <title> leave a search engine to guess which of them answers the query
// (developers.google.com/search/docs/appearance/title-link: "It's important to have distinct text
// that describes the content of the page in the <title> element for each page on your site").
// /benchmark and /help/benchmark shared one until 2026-09-25.
describe("page titles", () => {
  it.each([["tr", tr], ["en", en]] as const)("are distinct across every page in %s", (_, catalogue) => {
    const titles = Object.values(catalogue.metadata.pages as Record<string, { title?: string }>)
      .map((page) => page.title)
      .filter((title): title is string => title !== undefined);
    const repeated = titles.filter((title, index) => titles.indexOf(title) !== index);
    expect(repeated).toEqual([]);
  });
});

describe("landing page title", () => {
  it("still carries the term people actually search for", () => {
    // "takip" or its inflected "takibi" — the 2026-09-18 title says "iş başvuru takibi".
    expect(tr.metadata.pages.home.title.toLocaleLowerCase("tr")).toMatch(/başvuru taki[bp]/);
    expect(en.metadata.pages.home.title.toLowerCase()).toContain("application tracker");
  });

  it("keeps that term in the description too, alongside the promise", () => {
    expect(tr.metadata.pages.home.description.toLocaleLowerCase("tr")).toContain("başvuru takib");
    expect(en.metadata.pages.home.description.toLowerCase()).toContain("application tracking");
  });

  // 2026-09-18 (growth audit finding 06): the titles now also speak the words people type —
  // "ATS" for the CV check, "çalışan yorumları" / "employee reviews" for a company — because
  // that is what the competing results rank on. The H1s did not change; only what a search
  // engine matches did.
  it("names ATS on the pages a CV-check search should land on", () => {
    expect(tr.metadata.pages.home.title).toContain("ATS");
    expect(en.metadata.pages.home.title).toContain("ATS");
    expect(tr.metadata.pages.cvScan.title).toContain("ATS");
    expect(en.metadata.pages.cvScan.title).toContain("ATS");
  });

  it("calls a company page what people search for", () => {
    expect(tr.companies.page.metaTitle.toLocaleLowerCase("tr")).toContain("çalışan yorumları");
    expect(en.companies.page.metaTitle.toLowerCase()).toContain("employee reviews");
    expect(tr.metadata.pages.companies.title.toLocaleLowerCase("tr")).toContain("çalışan yorumları");
    expect(en.metadata.pages.companies.title.toLowerCase()).toContain("employee reviews");
  });
});

describe("share images", () => {
  // Until 2026-09-14 only the landing page had an og:image. This pins the fix: every page built
  // through buildMetadata carries a card, and the guide page puts the Organization node the Article
  // references into the same graph instead of leaving the @id dangling.
  const read = (relative: string) => readFileSync(path.join(process.cwd(), relative), "utf8");

  it("are set for every page that goes through buildMetadata", () => {
    const source = read("src/lib/seo/pageMetadata.ts");
    expect(source).toContain("ogImagePath(locale, cardTitle, kicker)");
    expect(source).toMatch(/openGraph:\s*\{[\s\S]*?images,/);
    expect(source).toMatch(/twitter:\s*\{[\s\S]*?images,/);
  });

  it("are the post's cover on a blog post when it is card-sized, else the generated card (2026-09-21)", () => {
    const page = read("src/app/[locale]/(public)/blog/[slug]/page.tsx");
    expect(page).toContain("coverIsShareImage(coverSize)");
    expect(page).toMatch(/image:\s*\{\s*url:\s*`\$\{SITE_URL\}\$\{post\.coverImageUrl\}`/);
    const source = read("src/lib/seo/pageMetadata.ts");
    expect(source).toContain("const images = [image ?? {");
  });

  it("show the hero line on the landing page's card, not its search title", () => {
    const page = read("src/app/[locale]/page.tsx");
    expect(page).toContain('shareTitle: tHero("title")');
    expect(existsSync(path.join(process.cwd(), "src/app/[locale]/opengraph-image.tsx"))).toBe(false);
  });

  // …unless the guide has a card-sized cover of its own: then the cover is the share image, the
  // same rule as a blog post (2026-09-26).
  it("are the image the guide article's JSON-LD names, next to the Organization it references", () => {
    const page = read("src/app/[locale]/(public)/guide/[slug]/page.tsx");
    expect(page).toContain("organizationJsonLd()");
    expect(page).toContain('ogImagePath(locale, guide.title, tSection("guide.title"))');
    expect(page).toContain("coverIsShareImage(coverSize)");
  });
});

describe("a guide slug under the wrong locale prefix", () => {
  const read = (relative: string) => readFileSync(path.join(process.cwd(), relative), "utf8");

  // Search Console listed /tr/guide/<english slug> and /en/guide/<turkish slug> as 404s. The
  // proxy answers them (and the locale-less /guide/<slug>) with a permanent redirect before the
  // page runs — for the guides that were files, whose addresses are in GUIDE_LINKS.
  it("is redirected by the proxy, not by the page", () => {
    const proxy = read("src/proxy.ts");
    expect(proxy).toContain("guideRedirectForPath(request.nextUrl.pathname)");
    expect(proxy).toMatch(/NextResponse\.redirect\(new URL\(`\$\{guideUrl\}\$\{request\.nextUrl\.search\}`, request\.url\), 301\)/);

    const page = read("src/app/[locale]/(public)/guide/[slug]/page.tsx");
    expect(page).not.toMatch(/permanentRedirect|redirect\(/);
  });

  // Since 2026-09-26 the guide is read per request, like the blog: dynamic, so an unknown slug's
  // notFound() is a 404 and not the static-page 500 of 2026-09-16.
  for (const page of ["src/app/[locale]/(public)/guide/page.tsx", "src/app/[locale]/(public)/guide/[slug]/page.tsx"]) {
    it(`${page} stays dynamic`, () => {
      const source = read(page);
      expect(source).not.toContain("generateStaticParams");
      expect(source).not.toContain("force-static");
      expect(source).not.toContain("dynamicParams");
    });
  }
});
