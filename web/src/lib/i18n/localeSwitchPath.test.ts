import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { routing } from "@/i18n/routing";
import { PUBLIC_PATHS } from "@/lib/seo/routes";
import { TRANSLATED_SLUGS, localeSwitchPath } from "./localeSwitchPath";

// next-intl's usePathname hands the switcher the path without its locale — the address the
// visitor sees (`/offer-comparison` under /en), not the Turkish route directory it is rewritten to.
describe("localeSwitchPath", () => {
  it.each([
    // [current path, target locale, expected]
    ["/offer-comparison", "tr", "/teklif-karsilastirma"],
    ["/teklif-karsilastirma", "en", "/offer-comparison"],
    ["/cv-scan", "tr", "/cv-tarama"],
    ["/cv-tarama", "en", "/cv-scan"],
    ["/cv-scan/score/88-28-22-20-18", "tr", "/cv-tarama/puan/88-28-22-20-18"],
    ["/about", "tr", "/hakkimizda"],
    ["/hakkimizda", "en", "/about"],
    ["/flow/abc", "tr", "/akis/abc"],
    ["/guide/where-did-my-applications-go", "tr", "/guide/basvurularim-nereye-gitti"],
    ["/guide/basvurularim-nereye-gitti", "en", "/guide/where-did-my-applications-go"],
  ])("sends %s to its %s address %s (the 2026-09-24 bug: /tr/offer-comparison)", (path, target, expected) => {
    expect(localeSwitchPath(path, target)).toBe(expected);
  });

  it("keeps a path that is the same in both languages", () => {
    expect(localeSwitchPath("/benchmark", "tr")).toBe("/benchmark");
    expect(localeSwitchPath("/help/offer-comparison", "en")).toBe("/help/offer-comparison");
    expect(localeSwitchPath("/dashboard", "en")).toBe("/dashboard");
    expect(localeSwitchPath("/", "tr")).toBe("/");
  });

  it("follows the page's own hreflang alternate first — the only way to a blog post's translation", () => {
    const alternates = { tr: "https://ekariyerim.com/tr/blog/merhaba", en: "https://ekariyerim.com/en/blog/hello" };
    expect(localeSwitchPath("/blog/hello", "tr", alternates)).toBe("/blog/merhaba");
  });

  it("lands on the blog index when a post has no translation in the target language", () => {
    expect(localeSwitchPath("/blog/hello", "tr", { en: "https://ekariyerim.com/en/blog/hello" })).toBe("/blog");
    expect(localeSwitchPath("/blog", "tr")).toBe("/blog");
  });

  it("ignores an alternate that points under another locale", () => {
    expect(localeSwitchPath("/offer-comparison", "tr", { tr: "https://ekariyerim.com/en/offer-comparison" })).toBe(
      "/teklif-karsilastirma",
    );
  });
});

// The two tripwires that keep this working for pages added later (2026-09-24). A page whose slug
// differs per language is listed in PUBLIC_PATHS (the sitemap) as a per-locale record, and the
// proxy redirects its wrong-language spelling. Both lists are checked against the switcher here, so
// a new translated page cannot ship with a language switch that lands on the wrong address.
describe("every translated page switches language correctly", () => {
  const translated = PUBLIC_PATHS.filter((entry): entry is Record<string, string> => typeof entry !== "string");

  it.each(translated.map((paths) => [Object.values(paths).join(" ↔ "), paths] as const))("%s", (_, paths) => {
    for (const from of routing.locales) {
      for (const to of routing.locales) {
        // No hreflang alternates passed: the table alone must get it right.
        expect(localeSwitchPath(paths[from], to), `${from} ${paths[from]} → ${to}`).toBe(paths[to]);
      }
    }
  });

  it("knows every translated-slug redirect the proxy knows", () => {
    const proxy = readFileSync(path.join(process.cwd(), "src/proxy.ts"), "utf8");
    const used = [...proxy.matchAll(/\b(\w+RedirectForPath)\(/g)]
      .map((match) => match[1])
      // Old blog slugs fixed by a migration: a rename inside one language, not a translation.
      .filter((name) => name !== "blogSlugRedirectForPath");
    const known = TRANSLATED_SLUGS.map((fn) => fn.name);
    expect([...new Set(used)].filter((name) => !known.includes(name))).toEqual([]);
  });
});
