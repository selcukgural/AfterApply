import type { MetadataRoute } from "next";
import { routing } from "@/i18n/routing";

const BASE_URL = "https://ekariyerim.com";

// The help centre is the only part of the site that answers a search query someone actually types
// ("linkedin başvuru geçmişi dışa aktarma"), so leaving its eleven pages out of the sitemap left
// the most findable content unlisted. Password reset and the OAuth callbacks stay out on purpose —
// they are single-use, per-request pages and carry robots: noindex.
const PUBLIC_PATHS = [
  "",
  "/login",
  "/register",
  "/privacy",
  "/extension-privacy",
  "/cookies",
  "/help",
  "/help/getting-started",
  "/help/dashboard",
  "/help/tracked-jobs",
  "/help/applications",
  "/help/suggestions",
  "/help/cv",
  "/help/import",
  "/help/settings",
  "/help/chrome-extension",
  "/help/faq",
];

export default function sitemap(): MetadataRoute.Sitemap {
  return routing.locales.flatMap((locale) =>
    PUBLIC_PATHS.map((path) => ({
      url: `${BASE_URL}/${locale}${path}`,
      lastModified: new Date(),
    })),
  );
}
