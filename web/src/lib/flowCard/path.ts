import { routing } from "@/i18n/routing";

/**
 * Where a shared flow card lands: `/tr/akis/<card>`, `/en/flow/<card>`. Localised the way the CV
 * scan's score pages are (lib/cvScan/path.ts): the English address is rewritten onto the Turkish
 * route directory in `next.config.ts`, the wrong-locale spelling is redirected from `proxy.ts`, and
 * a card that does not parse is turned into the 404 page there too, before the route renders.
 */
const FLOW_SLUGS: Record<string, string> = { tr: "akis", en: "flow" };

/** The page a shared card points at — locale-less, for next-intl's `Link` and `metadata`. */
export function flowCardPath(locale: string, card: string): string {
  return `/${FLOW_SLUGS[locale] ?? FLOW_SLUGS[routing.defaultLocale]}/${card}`;
}

/** `/en/flow/<card>` → `{ locale: "en", card }`, or null for any other path. */
export function flowCardOf(pathname: string): { locale: string; card: string } | null {
  const match = /^\/(tr|en)\/(?:akis|flow)\/([^/]+)\/?$/.exec(pathname);
  return match ? { locale: match[1], card: match[2] } : null;
}

/** `/tr/flow/<card>` → `/tr/akis/<card>` (permanent), or null when the slug is already right. */
export function flowCardRedirectForPath(pathname: string): string | null {
  const match = /^\/(tr|en)\/(akis|flow)\/([^/]+)\/?$/.exec(pathname);
  if (!match) return null;
  const [, locale, slug, card] = match;
  return slug === FLOW_SLUGS[locale] ? null : `/${locale}${flowCardPath(locale, card)}`;
}
