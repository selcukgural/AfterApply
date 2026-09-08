import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";

const SITE_NAME = "e-kariyerim";

/**
 * Per-page title/description/canonical for the public pages.
 *
 * Without this every public page inherits the root layout's metadata, so the privacy policy, the
 * cookie policy and all eleven help pages ship the same <title> ("e-kariyerim") and the same
 * one-line description — indistinguishable to a search engine and to anyone sharing the link.
 *
 * `path` is the locale-less path ("/help/faq"); `key` names an entry under `metadata.pages`.
 */
export async function pageMetadata(
  locale: string,
  path: string,
  key: string,
  options: { index?: boolean } = {},
): Promise<Metadata> {
  const t = await getTranslations("metadata.pages");
  const title = `${t(`${key}.title`)} · ${SITE_NAME}`;
  const description = t(`${key}.description`);
  const url = `/${locale}${path}`;

  return {
    title,
    description,
    alternates: {
      canonical: url,
      languages: { tr: `/tr${path}`, en: `/en${path}` },
    },
    // Password reset and OAuth callbacks are per-request, single-use pages: indexing them puts a
    // dead link in the results and nothing useful on the page behind it.
    ...(options.index === false ? { robots: { index: false, follow: false } } : {}),
    openGraph: {
      title,
      description,
      type: "website",
      url,
      siteName: SITE_NAME,
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
    },
  };
}
