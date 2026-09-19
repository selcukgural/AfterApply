import type { MetadataRoute } from "next";
import { routing } from "@/i18n/routing";
import { PUBLIC_PATHS, SITE_URL, alternateLanguages, pathFor } from "@/lib/seo/routes";
import { fetchReviewedSlugs } from "@/lib/companies/publicApi.server";
import { fetchBlogSlugs } from "@/lib/blog/publicApi.server";
import { BLOG_PATH, blogAlternates, blogPostPath } from "@/lib/blog/blogPaths";
import type { BlogSlug } from "@/types/api";

/** The static public pages: everything in PUBLIC_PATHS, in every locale. */
export function staticSitemapEntries(): MetadataRoute.Sitemap {
  return routing.locales.flatMap((locale) =>
    PUBLIC_PATHS.map((path) => ({
      url: `${SITE_URL}/${locale}${pathFor(path, locale)}`,
      // No `lastModified`. This route is rendered per request, so `new Date()` reported every one
      // of these URLs as changed on every crawl — a signal Google learns to ignore outright. An
      // absent lastmod is read as "use your own crawl history", which is the honest answer until
      // there is a real per-page modification date to give.
      alternates: { languages: alternateLanguages(path, SITE_URL) },
    })),
  );
}

/**
 * The company pages with at least one published review, in every locale. These are the one kind
 * of entry with a real modification date — the last approval — so they carry one. Companies
 * nobody has reviewed are left out: their page says noindex, and listing it would contradict that.
 */
export function companySitemapEntries(companies: readonly { slug: string; lastApprovedAt: string }[]): MetadataRoute.Sitemap {
  return routing.locales.flatMap((locale) =>
    companies.map((company) => {
      const path = `/companies/${company.slug}`;
      return {
        url: `${SITE_URL}/${locale}${path}`,
        lastModified: new Date(company.lastApprovedAt),
        alternates: { languages: alternateLanguages(path, SITE_URL) },
      };
    }),
  );
}

/**
 * The blog (2026-09-19): one index per language that has a published post, and every post under
 * its own language with the last publish as `lastModified`. Nothing at all while nothing is
 * published — the index 404s then, and `/blog` is deliberately not in PUBLIC_PATHS for that
 * reason. A post's hreflang set names only the languages it exists in (blogAlternates).
 */
export function blogSitemapEntries(posts: readonly BlogSlug[]): MetadataRoute.Sitemap {
  const languages = routing.locales.filter((locale) => posts.some((post) => post.language === locale));
  const indexes: MetadataRoute.Sitemap = languages.map((language) => ({
    url: `${SITE_URL}/${language}${BLOG_PATH}`,
    alternates: {
      languages: Object.fromEntries([
        ...languages.map((other) => [other, `${SITE_URL}/${other}${BLOG_PATH}`]),
        ["x-default", `${SITE_URL}/${languages.includes(routing.defaultLocale) ? routing.defaultLocale : languages[0]}${BLOG_PATH}`],
      ]),
    },
  }));
  const pages: MetadataRoute.Sitemap = posts.map((post) => ({
    url: `${SITE_URL}/${post.language}${blogPostPath(post.slug)}`,
    lastModified: new Date(post.updatedAt),
    alternates: { languages: blogAlternates(post, SITE_URL) },
  }));
  return [...indexes, ...pages];
}

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  // Both fetchers answer [] when the API is unreachable, so the sitemap degrades to the static
  // list rather than failing the crawl.
  const [companies, posts] = await Promise.all([fetchReviewedSlugs(), fetchBlogSlugs()]);
  return [...staticSitemapEntries(), ...companySitemapEntries(companies), ...blogSitemapEntries(posts)];
}
