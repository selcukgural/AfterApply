import { routing } from "@/i18n/routing";

/**
 * The offer comparison's address in each language, translated like the CV scan's and the about
 * page's (`lib/cvScan/path.ts`): the route directory is the Turkish slug — "teklif karşılaştırma"
 * is what the page's audience types — `/en/offer-comparison` is rewritten onto it in
 * `next.config.ts`, and the wrong-locale spelling is redirected from `proxy.ts`.
 */
export const OFFER_COMPARE_PATHS: Record<string, string> = { tr: "/teklif-karsilastirma", en: "/offer-comparison" };

export function offerComparePath(locale: string): string {
  return OFFER_COMPARE_PATHS[locale] ?? OFFER_COMPARE_PATHS[routing.defaultLocale];
}

/** `/en/teklif-karsilastirma` → `/en/offer-comparison`, and back for `/tr`; null otherwise. */
export function offerCompareRedirectForPath(pathname: string): string | null {
  const match = /^\/(tr|en)\/(teklif-karsilastirma|offer-comparison)\/?$/.exec(pathname);
  if (!match) return null;
  const [, locale, slug] = match;
  const right = offerComparePath(locale);
  return `/${slug}` === right ? null : `/${locale}${right}`;
}
