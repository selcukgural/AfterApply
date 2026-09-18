import { routing } from "@/i18n/routing";

/**
 * The about page's address in each language — the second public page after the CV scan with a
 * translated slug, handled the same way (`lib/cvScan/path.ts`): the route directory is the
 * Turkish slug, `/en/about` is rewritten onto it in `next.config.ts`, and the wrong-locale
 * spelling is redirected from `proxy.ts`.
 */
export const ABOUT_PATHS: Record<string, string> = { tr: "/hakkimizda", en: "/about" };

export function aboutPath(locale: string): string {
  return ABOUT_PATHS[locale] ?? ABOUT_PATHS[routing.defaultLocale];
}

/** `/en/hakkimizda` → `/en/about`, `/tr/about` → `/tr/hakkimizda`; null otherwise. */
export function aboutRedirectForPath(pathname: string): string | null {
  const match = /^\/(tr|en)\/(hakkimizda|about)\/?$/.exec(pathname);
  if (!match) return null;
  const [, locale, slug] = match;
  const right = aboutPath(locale);
  return `/${slug}` === right ? null : `/${locale}${right}`;
}
