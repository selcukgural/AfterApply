import type { MetadataRoute } from "next";
import { routing } from "@/i18n/routing";
import { PUBLIC_PATHS, SITE_URL, alternateLanguages, pathFor } from "@/lib/seo/routes";

export default function sitemap(): MetadataRoute.Sitemap {
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
