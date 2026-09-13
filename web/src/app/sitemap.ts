import type { MetadataRoute } from "next";
import { routing } from "@/i18n/routing";
import { PUBLIC_PATHS, SITE_URL, alternateLanguages, pathFor } from "@/lib/seo/routes";
import { fetchReviewedSlugs } from "@/lib/companies/publicApi.server";

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

export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  // fetchReviewedSlugs answers [] when the API is unreachable, so the sitemap degrades to the
  // static list rather than failing the crawl.
  return [...staticSitemapEntries(), ...companySitemapEntries(await fetchReviewedSlugs())];
}
