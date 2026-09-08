import type { MetadataRoute } from "next";
import { routing } from "@/i18n/routing";
import { PROTECTED_PATHS, SITE_URL, disallowedPaths } from "@/lib/seo/routes";

export default function robots(): MetadataRoute.Robots {
  return {
    rules: {
      userAgent: "*",
      allow: "/",
      disallow: disallowedPaths(routing.locales, PROTECTED_PATHS),
    },
    sitemap: `${SITE_URL}/sitemap.xml`,
  };
}
