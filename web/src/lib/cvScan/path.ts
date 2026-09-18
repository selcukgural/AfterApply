import { routing } from "@/i18n/routing";

/**
 * The CV scan's address in each language — the one public page besides the guide articles whose
 * slug is translated (growth audit 2026-09-14, finding 14: `/en/cv-tarama` was a Turkish slug on
 * an English page). Localised the way the guide does it rather than through next-intl's
 * `pathnames`, which would retype every `Link` href in the app: the English address is rewritten
 * onto the Turkish route directory in `next.config.ts`, the wrong-locale addresses are redirected
 * from `proxy.ts`, and every link goes through `cvScanPath`.
 *
 * The score pages (`/cv-tarama/puan/88`, `/cv-scan/score/88`) hang off the same slugs.
 */
export const CV_SCAN_PATHS: Record<string, string> = { tr: "/cv-tarama", en: "/cv-scan" };

const SCORE_SEGMENTS: Record<string, string> = { tr: "puan", en: "score" };

export function cvScanPath(locale: string): string {
  return CV_SCAN_PATHS[locale] ?? CV_SCAN_PATHS[routing.defaultLocale];
}

/** The public page a shared score points at — locale-less, for next-intl's `Link`. */
export function cvScanScorePath(locale: string, card: string): string {
  return `${cvScanPath(locale)}/${SCORE_SEGMENTS[locale] ?? SCORE_SEGMENTS[routing.defaultLocale]}/${card}`;
}

/**
 * Where a scan address under the wrong slug for its locale should go, permanently — or null when
 * the path is not a scan address or is already right. `/en/cv-tarama` → `/en/cv-scan`,
 * `/tr/cv-scan/score/88` → `/tr/cv-tarama/puan/88`.
 */
/**
 * The card segment of a score-page address, with its locale — `/en/cv-scan/score/88` →
 * `{ locale: "en", card: "88" }` — or null for any other path. The proxy uses it to send a card
 * the scan could not have produced (`parseScoreCard` → null) to the 404 page *before* the route
 * renders: a `notFound()` inside the page itself turns a prerendered page dynamic at runtime,
 * which Next answers with a 500 in production (DECISIONS.md 2026-09-16).
 */
export function cvScanScoreCardOf(pathname: string): { locale: string; card: string } | null {
  const match = /^\/(tr|en)\/(?:cv-tarama|cv-scan)\/(?:puan|score)\/([^/]+)\/?$/.exec(pathname);
  return match ? { locale: match[1], card: match[2] } : null;
}

export function cvScanRedirectForPath(pathname: string): string | null {
  const match = /^\/(tr|en)\/(cv-tarama|cv-scan)(?:\/(puan|score)\/([^/]+))?\/?$/.exec(pathname);
  if (!match) return null;

  const [, locale, slug, segment, card] = match;
  const rightSlug = cvScanPath(locale).slice(1);
  const rightSegment = SCORE_SEGMENTS[locale];
  if (slug === rightSlug && (segment === undefined || segment === rightSegment)) return null;

  return card === undefined ? `/${locale}/${rightSlug}` : `/${locale}/${rightSlug}/${rightSegment}/${card}`;
}
