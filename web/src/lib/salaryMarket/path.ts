import { routing } from "@/i18n/routing";

/**
 * The salary pages' address in each language, translated like the offer comparison's
 * (`lib/offerCompare/path.ts`): the route directory is the Turkish slug — "maaşlar" is what the
 * page's audience types — `/en/salaries` (and `/en/salaries/<occupation>`) is rewritten onto it
 * in `next.config.ts`, and the wrong-locale spelling is redirected from `proxy.ts`. The
 * occupation slug itself is the same in both languages.
 */
export const SALARY_MARKET_PATHS: Record<string, string> = { tr: "/maaslar", en: "/salaries" };

export function salaryMarketPath(locale: string): string {
  return SALARY_MARKET_PATHS[locale] ?? SALARY_MARKET_PATHS[routing.defaultLocale];
}

export function salaryOccupationPath(locale: string, slug: string): string {
  return `${salaryMarketPath(locale)}/${slug}`;
}

/** The per-locale set for one occupation page — its canonical and hreflang pair. */
export function salaryOccupationPaths(slug: string): Record<string, string> {
  return Object.fromEntries(routing.locales.map((locale) => [locale, salaryOccupationPath(locale, slug)]));
}

/** `/en/maaslar/cto` → `/en/salaries/cto`, `/tr/salaries` → `/tr/maaslar`; null otherwise. */
export function salaryMarketRedirectForPath(pathname: string): string | null {
  const match = /^\/(tr|en)\/(maaslar|salaries)(\/[a-z0-9-]+)?\/?$/.exec(pathname);
  if (!match) return null;
  const [, locale, slug, rest = ""] = match;
  const right = salaryMarketPath(locale);
  return `/${slug}` === right ? null : `/${locale}${right}${rest}`;
}
