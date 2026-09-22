import { BLOG_PATH, blogPostPath } from "./blogPaths";

/**
 * Old blog addresses that must keep working after a post's slug was corrected. A published slug
 * is locked (DECISIONS.md 2026-09-19: a live URL is a promise); the only way one changes is a
 * data migration that renames it, and the old spelling is then listed here so every link and
 * index entry that already points at it lands on the new one with a 301.
 *
 * Keyed by language, then old slug → new slug. Add a row in the same change as the migration.
 */
export const BLOG_SLUG_REDIRECTS: Record<string, Record<string, string>> = {
  en: {
    // Typo in the generated slug ("kep"), fixed 2026-09-22 by migration FixEnglishMotivationPostSlug.
    "how-can-i-kep-my-motivation-while-job-searching": "how-can-i-keep-my-motivation-while-job-searching",
  },
};

/** `/en/blog/<old slug>` → `/en/blog/<new slug>`; null for every other path. */
export function blogSlugRedirectForPath(pathname: string): string | null {
  const match = new RegExp(`^/(tr|en)${BLOG_PATH}/([a-z0-9-]+)/?$`).exec(pathname);
  if (!match) return null;
  const [, locale, slug] = match;
  const target = BLOG_SLUG_REDIRECTS[locale]?.[slug];
  return target ? `/${locale}${blogPostPath(target)}` : null;
}
