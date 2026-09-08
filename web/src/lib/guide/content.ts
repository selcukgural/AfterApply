import type { ComponentType } from "react";
import type { GuideLocale } from "@/lib/guide/articles";

type GuideModule = { default: ComponentType };
type GuideLoader = () => Promise<GuideModule>;

/**
 * Where each article's prose lives.
 *
 * Written out rather than built from a template (`import(\`…/${key}.${locale}.mdx\`)`) so every
 * path is a literal the bundler can see: a key with no .mdx file behind it fails the build here,
 * instead of 404-ing in production for the one locale nobody checked. `articles.test.ts` holds the
 * two lists to the same set of keys.
 */
const GUIDE_LOADERS: Record<string, Record<GuideLocale, GuideLoader>> = {
  "linkedin-application-history": {
    tr: () => import("@/content/guide/linkedin-application-history.tr.mdx"),
    en: () => import("@/content/guide/linkedin-application-history.en.mdx"),
  },
  "application-tracker-spreadsheet": {
    tr: () => import("@/content/guide/application-tracker-spreadsheet.tr.mdx"),
    en: () => import("@/content/guide/application-tracker-spreadsheet.en.mdx"),
  },
  "response-time": {
    tr: () => import("@/content/guide/response-time.tr.mdx"),
    en: () => import("@/content/guide/response-time.en.mdx"),
  },
  "rejection-email": {
    tr: () => import("@/content/guide/rejection-email.tr.mdx"),
    en: () => import("@/content/guide/rejection-email.en.mdx"),
  },
  "kariyer-net-application-history": {
    tr: () => import("@/content/guide/kariyer-net-application-history.tr.mdx"),
    en: () => import("@/content/guide/kariyer-net-application-history.en.mdx"),
  },
  "reapplying-to-the-same-company": {
    tr: () => import("@/content/guide/reapplying-to-the-same-company.tr.mdx"),
    en: () => import("@/content/guide/reapplying-to-the-same-company.en.mdx"),
  },
  "how-many-applications": {
    tr: () => import("@/content/guide/how-many-applications.tr.mdx"),
    en: () => import("@/content/guide/how-many-applications.en.mdx"),
  },
};

export const GUIDE_LOADER_KEYS = Object.keys(GUIDE_LOADERS);

export async function loadGuideArticle(key: string, locale: GuideLocale): Promise<ComponentType> {
  const loader = GUIDE_LOADERS[key]?.[locale];
  if (!loader) throw new Error(`No guide article body for "${key}" in "${locale}"`);
  const loaded = await loader();
  return loaded.default;
}
