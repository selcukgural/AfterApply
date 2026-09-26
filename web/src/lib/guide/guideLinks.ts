import { routing } from "@/i18n/routing";

/**
 * What the site still needs to know about guide articles without asking the API (2026-09-26).
 *
 * The articles themselves live in the database now, written in the blog's editor (DECISIONS.md
 * 2026-09-26); the pages read them from the API. Three things cannot wait for a fetch, and they
 * are all this file is for:
 *
 * - the product screens that link to a particular guide (the review form's rules, a company
 *   page's review list, the help centre, the flow card) — `guidePath(key, locale)`;
 * - the proxy's 301 for an address in the wrong language, which runs before any page does;
 * - the language switcher's fallback for the same.
 *
 * The table holds the ten guides that were files until 2026-09-26, keyed as they were. A slug is
 * locked once a guide has been published, so these addresses cannot drift from the database. A
 * guide written in the editor after that is found through its own page — hreflang and the
 * sitemap — and needs no entry here unless the product links to it by name.
 */

export const GUIDE_LOCALES = ["tr", "en"] as const;

export type GuideLocale = (typeof GUIDE_LOCALES)[number];

export const GUIDE_PATH = "/guide";

export const GUIDE_LINKS = {
  "application-flow": { tr: "basvurularim-nereye-gitti", en: "where-did-my-applications-go" },
  "writing-a-fair-review": { tr: "adil-ve-faydali-bir-degerlendirme-yazmak", en: "writing-a-fair-review" },
  "reading-employee-reviews": { tr: "calisan-deneyimlerini-nasil-okumali", en: "how-to-read-employee-reviews" },
  "linkedin-application-history": { tr: "linkedin-basvuru-gecmisi-nasil-indirilir", en: "export-linkedin-application-history" },
  "application-tracker-spreadsheet": { tr: "is-basvuru-takip-excel-sablonu", en: "job-application-tracker-spreadsheet" },
  "response-time": { tr: "is-basvurusundan-sonra-ne-kadar-beklenir", en: "how-long-to-wait-after-applying" },
  "rejection-email": { tr: "basvurunuz-olumsuz-sonuclandi-ne-demek", en: "what-a-rejection-email-means" },
  "kariyer-net-application-history": { tr: "kariyer-net-basvurularim-nerede", en: "kariyer-net-application-history" },
  "reapplying-to-the-same-company": { tr: "ayni-sirkete-tekrar-basvurmak", en: "reapplying-to-the-same-company" },
  "how-many-applications": { tr: "kac-is-basvurusu-yapmak-gerekir", en: "how-many-job-applications" },
} as const satisfies Record<string, Record<GuideLocale, string>>;

export type GuideKey = keyof typeof GUIDE_LINKS;

export function isGuideLocale(locale: string): locale is GuideLocale {
  return (GUIDE_LOCALES as readonly string[]).includes(locale);
}

/**
 * The path of a guide by key, for the product screens that link into the guide. A key nobody
 * registered is a programming error, so it throws rather than rendering a link to nowhere (the
 * test suite scans every call, so it never reaches a page); a locale the guide does not have
 * falls back to the default one, as every other localised path does.
 */
export function guidePath(key: string, locale: string): string {
  const slugs = (GUIDE_LINKS as Record<string, Record<GuideLocale, string>>)[key];
  if (!slugs) throw new Error(`No guide article "${key}"`);
  return `${GUIDE_PATH}/${slugs[isGuideLocale(locale) ? locale : (routing.defaultLocale as GuideLocale)]}`;
}

/**
 * Where a guide address in the wrong language should go, or null when it is fine (or not one of
 * these guides). Two shapes, both from before the move and both still in search results:
 *
 * - `/<locale>/guide/<slug>` where the slug is the other locale's: the locale in the URL wins, so
 *   the reader gets that locale's article at its own slug;
 * - `/guide/<slug>` with no locale (the links inside the guides' own text are written that way):
 *   the slug says which language the reader wants, so that language wins.
 */
export function guideRedirectForPath(pathname: string): string | null {
  const prefixed = /^\/(tr|en)\/guide\/([^/]+)\/?$/.exec(pathname);
  if (prefixed) {
    const locale = prefixed[1] as GuideLocale;
    const slug = decodeURIComponent(prefixed[2]);
    for (const slugs of Object.values(GUIDE_LINKS)) {
      if (slugs[locale] === slug) return null;
      if (Object.values(slugs).includes(slug as never)) return `/${locale}${GUIDE_PATH}/${slugs[locale]}`;
    }
    return null;
  }

  const unprefixed = /^\/guide\/([^/]+)\/?$/.exec(pathname);
  if (!unprefixed) return null;

  const slug = decodeURIComponent(unprefixed[1]);
  for (const slugs of Object.values(GUIDE_LINKS)) {
    for (const locale of GUIDE_LOCALES) {
      if (slugs[locale] === slug) return `/${locale}${GUIDE_PATH}/${slug}`;
    }
  }
  return null;
}
