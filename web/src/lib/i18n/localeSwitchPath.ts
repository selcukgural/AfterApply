import { aboutRedirectForPath } from "@/lib/about/path";
import { cvScanRedirectForPath } from "@/lib/cvScan/path";
import { flowCardRedirectForPath } from "@/lib/flowCard/path";
import { guideRedirectForPath } from "@/lib/guide/articles";
import { offerCompareRedirectForPath } from "@/lib/offerCompare/path";
import { BLOG_PATH } from "@/lib/blog/blogPaths";

/**
 * The pages whose slug differs per language, each able to say where a wrong-language spelling
 * belongs (`/tr/offer-comparison` → `/tr/teklif-karsilastirma`). The proxy answers the same
 * question with a 301 for a visitor who types the address; this list lets the language switcher
 * compute the right address before it navigates, instead of leaning on that redirect.
 */
export const TRANSLATED_SLUGS: readonly ((pathname: string) => string | null)[] = [
  guideRedirectForPath,
  cvScanRedirectForPath,
  flowCardRedirectForPath,
  aboutRedirectForPath,
  offerCompareRedirectForPath,
];

/** `/tr/foo` → `/foo` and `/tr` → `/` for the given locale; null when the path is under another one. */
function withoutLocale(pathname: string, locale: string): string | null {
  if (pathname === `/${locale}` || pathname === `/${locale}/`) return "/";
  return pathname.startsWith(`/${locale}/`) ? pathname.slice(locale.length + 1) : null;
}

/**
 * Where "this page in the other language" is, as a locale-less path for next-intl's router.
 *
 * Until 2026-09-24 the switcher kept the path and swapped only the locale, so English
 * `/offer-comparison` became `/tr/offer-comparison` — a Turkish address that exists only as a
 * redirect, and a client-side navigation does not always follow it (a signed-in user was left on
 * the wrong address). In order:
 *
 * 1. the page's own `hreflang` alternate for the target language — the page knows its
 *    counterpart best, and it is the only way to find a blog post's translation, whose slug is
 *    its own;
 * 2. the translated-slug table above;
 * 3. a blog post with no linked translation goes to that language's blog index rather than to a
 *    slug that does not exist there;
 * 4. every other path is the same in both languages.
 */
export function localeSwitchPath(pathname: string, target: string, alternates: Record<string, string> = {}): string {
  const alternate = alternates[target];
  if (alternate) {
    const path = withoutLocale(new URL(alternate, "https://ekariyerim.com").pathname, target);
    if (path) return path;
  }

  const full = pathname === "/" ? `/${target}` : `/${target}${pathname}`;
  for (const redirect of TRANSLATED_SLUGS) {
    const fixed = redirect(full);
    if (fixed) return withoutLocale(fixed, target) ?? pathname;
  }

  if (new RegExp(`^${BLOG_PATH}/[^/]+/?$`).test(pathname)) return BLOG_PATH;
  return pathname;
}

/** The `hreflang` alternates the current page declares, language → absolute URL. */
export function readAlternates(doc: Document): Record<string, string> {
  const result: Record<string, string> = {};
  doc.querySelectorAll<HTMLLinkElement>('link[rel="alternate"][hreflang]').forEach((link) => {
    const language = link.getAttribute("hreflang");
    if (language && language !== "x-default" && link.href) result[language] = link.href;
  });
  return result;
}
