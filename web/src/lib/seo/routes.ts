import { routing } from "@/i18n/routing";
import { GUIDE_ARTICLES, GUIDE_PATH, articlePaths } from "@/lib/guide/articles";

export const SITE_URL = "https://ekariyerim.com";
export const SITE_NAME = "e-kariyerim";

/** The help topics, in sidebar order — also the paths the sitemap and the breadcrumbs share. */
export const HELP_TOPICS = [
  { href: "/help", key: "overview" },
  { href: "/help/getting-started", key: "gettingStarted" },
  { href: "/help/dashboard", key: "dashboard" },
  { href: "/help/tracked-jobs", key: "trackedJobs" },
  { href: "/help/applications", key: "applications" },
  { href: "/help/cv", key: "cv" },
  { href: "/help/suggestions", key: "suggestions" },
  { href: "/help/import", key: "import" },
  { href: "/help/settings", key: "settings" },
  { href: "/help/chrome-extension", key: "chromeExtension" },
  { href: "/help/faq", key: "faq" },
] as const;

/**
 * A path that is either the same in every locale ("/help/faq") or translated per locale
 * ({ tr: "/guide/…", en: "/guide/…" }).
 */
export type LocalisedPath = string | Record<string, string>;

export function pathFor(path: LocalisedPath, locale: string): string {
  return typeof path === "string" ? path : (path[locale] ?? path[routing.defaultLocale]);
}

/**
 * Everything a search engine should see. The help centre is the only part of the site that answers
 * a search query someone actually types ("linkedin başvuru geçmişi dışa aktarma"), so its pages
 * carry most of the weight here. Password reset and the OAuth callbacks stay out on purpose —
 * they are single-use, per-request pages and carry robots: noindex.
 */
export const PUBLIC_PATHS: LocalisedPath[] = [
  "",
  "/login",
  "/register",
  "/privacy",
  "/extension-privacy",
  "/cookies",
  ...HELP_TOPICS.map((topic) => topic.href),
  GUIDE_PATH,
  // The guide articles are the one place where the path itself differs per locale: the slug is
  // where the search terms live, so it is translated rather than shared.
  ...GUIDE_ARTICLES.map(articlePaths),
];

/**
 * The signed-in areas. They redirect to the login page for a crawler, so nothing leaks either way,
 * but there is no reason to spend crawl budget on them.
 */
export const PROTECTED_PATHS = [
  "/dashboard",
  "/applications",
  "/tracked-jobs",
  "/suggestions",
  "/cv",
  "/import",
  "/notifications",
  "/settings",
  "/admin",
];

/**
 * robots.txt paths for the signed-in areas.
 *
 * `routing.localePrefix` is "always", so every URL that exists is `/tr/…` or `/en/…`. The bare
 * "/dashboard" this file used to disallow matched no real URL at all — the rule was inert.
 */
export function disallowedPaths(locales: readonly string[], paths: readonly string[]): string[] {
  return locales.flatMap((locale) => paths.map((path) => `/${locale}${path}`));
}

/**
 * The hreflang set for one locale-less path.
 *
 * `x-default` is what a search engine falls back to when the visitor's language is neither of ours;
 * without it Google picks one itself. It points at Turkish because that is `routing.defaultLocale`
 * and what `/` redirects to.
 *
 * `base` is empty for `Metadata.alternates` (resolved against `metadataBase`) and the absolute site
 * URL for the sitemap, which has no base to resolve against.
 */
export function alternateLanguages(path: LocalisedPath, base = ""): Record<string, string> {
  const entries = Object.fromEntries(
    routing.locales.map((locale) => [locale, `${base}/${locale}${pathFor(path, locale)}`]),
  );
  return { ...entries, "x-default": `${base}/${routing.defaultLocale}${pathFor(path, routing.defaultLocale)}` };
}
