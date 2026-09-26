import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { articleJsonLd, breadcrumbJsonLd, jsonLdGraph, organizationJsonLd } from "@/lib/seo/jsonLd";
import { ogImagePath } from "@/lib/seo/ogImage";
import { coverIsShareImage } from "@/lib/seo/shareImage";
import { GUIDE_PATH, isGuideLocale } from "@/lib/guide/guideLinks";
import { postAlternates, postPath } from "@/lib/blog/blogPaths";
import { fetchGuide } from "@/lib/blog/publicApi.server";
import { wordCount } from "@/lib/blog/seoChecks";
import { SITE_NAME, SITE_URL } from "@/lib/seo/routes";
import { GuideArticle } from "@/components/guide/GuideArticle";

export async function generateMetadata({ params }: PageProps<"/[locale]/guide/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  if (!isGuideLocale(locale)) return {};

  const guide = await fetchGuide(locale, slug);
  if (!guide) return {};

  const tSection = await getTranslations("metadata.pages");
  const coverSize = { width: guide.coverWidth, height: guide.coverHeight };
  return buildMetadata({
    locale,
    path: postPath("Guide", slug),
    // The slug differs per language: the hreflang pair is built from the guide and its linked
    // translation, not from "this path in every locale".
    languages: postAlternates("Guide", guide),
    title: guide.seoTitle || guide.title,
    description: guide.excerpt || guide.title,
    article: { publishedTime: guide.publishedAt, modifiedTime: guide.updatedAt },
    kicker: tSection("guide.title"),
    ...(guide.coverImageUrl && coverIsShareImage(coverSize)
      ? { image: { url: `${SITE_URL}${guide.coverImageUrl}`, width: guide.coverWidth!, height: guide.coverHeight!, alt: guide.coverAlt ?? guide.title } }
      : {}),
  });
}

/**
 * One guide article (2026-09-26: from the database, written in the blog's editor). Rendered on
 * every request from a `no-store` fetch — the API caches the published version and evicts on
 * every publish — so the page is dynamic and `notFound()` is a real 404, not the static-page 500
 * of 2026-09-16. An address in the wrong language never reaches here for the guides that were
 * files: the proxy redirects it first (`guideRedirectForPath`). The article is `GuideArticle`,
 * shared with the editor's preview; this page adds only what a crawler reads.
 */
export default async function GuideArticlePage({ params }: PageProps<"/[locale]/guide/[slug]">) {
  const { locale, slug } = await params;
  if (!isGuideLocale(locale)) notFound();

  const guide = await fetchGuide(locale, slug);
  if (!guide) notFound();

  const tSection = await getTranslations("metadata.pages");
  const path = postPath("Guide", guide.slug);
  const url = `${SITE_URL}/${locale}${path}`;
  const image = guide.coverImageUrl
    ? `${SITE_URL}${guide.coverImageUrl}`
    : `${SITE_URL}${ogImagePath(locale, guide.title, tSection("guide.title"))}`;

  return (
    <>
      <JsonLd
        data={jsonLdGraph(
          organizationJsonLd(),
          breadcrumbJsonLd(locale, [
            { name: SITE_NAME, path: "" },
            { name: tSection("guide.title"), path: GUIDE_PATH },
            { name: guide.title, path },
          ]),
          articleJsonLd({
            locale,
            path,
            headline: guide.title,
            description: guide.excerpt || guide.title,
            datePublished: guide.publishedAt,
            dateModified: guide.updatedAt,
            image,
            keywords: guide.keywords,
            wordCount: wordCount(guide.contentHtml),
          }),
        )}
      />
      <GuideArticle post={guide} url={url} />
    </>
  );
}
