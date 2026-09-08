import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { SITE_NAME, alternateLanguages, pathFor, type LocalisedPath } from "@/lib/seo/routes";

/**
 * Per-page title/description/canonical for the public pages.
 *
 * Without this every public page inherits the root layout's metadata, so the privacy policy, the
 * cookie policy and all eleven help pages ship the same <title> ("e-kariyerim") and the same
 * one-line description — indistinguishable to a search engine and to anyone sharing the link.
 */

export type PageMetadataOptions = {
  locale: string;
  /** Locale-less path ("/help/faq"), or the per-locale set when the slug itself is translated. */
  path: LocalisedPath;
  /** Without the site name — this adds it. */
  title: string;
  description: string;
  index?: boolean;
  /** Set for the guide articles, so a result can show when the piece was written. */
  article?: { publishedTime: string; modifiedTime?: string };
};

export function buildMetadata({ locale, path, title, description, index, article }: PageMetadataOptions): Metadata {
  const fullTitle = `${title} · ${SITE_NAME}`;
  const url = `/${locale}${pathFor(path, locale)}`;

  return {
    title: fullTitle,
    description,
    alternates: {
      canonical: url,
      languages: alternateLanguages(path),
    },
    // Password reset and OAuth callbacks are per-request, single-use pages: indexing them puts a
    // dead link in the results and nothing useful on the page behind it.
    ...(index === false ? { robots: { index: false, follow: false } } : {}),
    openGraph: {
      title: fullTitle,
      description,
      type: article ? "article" : "website",
      url,
      siteName: SITE_NAME,
      ...(article ? { publishedTime: article.publishedTime, modifiedTime: article.modifiedTime } : {}),
    },
    twitter: {
      card: "summary_large_image",
      title: fullTitle,
      description,
    },
  };
}

/** The same, for a page whose title and description are UI strings under `metadata.pages`. */
export async function pageMetadata(
  locale: string,
  path: LocalisedPath,
  key: string,
  options: { index?: boolean } = {},
): Promise<Metadata> {
  const t = await getTranslations("metadata.pages");
  return buildMetadata({
    locale,
    path,
    title: t(`${key}.title`),
    description: t(`${key}.description`),
    index: options.index,
  });
}
