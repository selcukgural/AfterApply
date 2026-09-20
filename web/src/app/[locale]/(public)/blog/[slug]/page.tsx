import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { routing } from "@/i18n/routing";
import type { BlogLanguage } from "@/types/api";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { SITE_NAME, SITE_URL } from "@/lib/seo/routes";
import { ogImagePath } from "@/lib/seo/ogImage";
import { JsonLd } from "@/components/seo/JsonLd";
import { articleJsonLd, breadcrumbJsonLd, jsonLdGraph, organizationJsonLd } from "@/lib/seo/jsonLd";
import { BLOG_PATH, blogAlternates, blogPostPath } from "@/lib/blog/blogPaths";
import { fetchBlogComments, fetchBlogPost } from "@/lib/blog/publicApi.server";
import { BlogArticle } from "@/components/blog/BlogArticle";

function isBlogLanguage(locale: string): locale is BlogLanguage {
  return (routing.locales as readonly string[]).includes(locale);
}

export async function generateMetadata({ params }: PageProps<"/[locale]/blog/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  if (!isBlogLanguage(locale)) return {};

  const post = await fetchBlogPost(locale, slug);
  if (!post) return {};

  const tSection = await getTranslations("metadata.pages");
  return buildMetadata({
    locale,
    path: blogPostPath(slug),
    // A post exists in one language (plus a linked translation): the hreflang set is built from
    // the post, not from "this path in every locale".
    languages: blogAlternates(post),
    title: post.title,
    description: post.excerpt || post.title,
    article: { publishedTime: post.publishedAt, modifiedTime: post.updatedAt },
    kicker: tSection("blog.title"),
  });
}

/**
 * One post. Server-rendered from a `no-store` fetch — the API serves the published version from
 * its cache and evicts on every publish — so the page is dynamic and `notFound()` is a real 404
 * here. The article itself is `BlogArticle`, shared with the editor's preview; this page adds
 * only what a crawler reads (metadata, JSON-LD).
 */
export default async function BlogPostPage({ params }: PageProps<"/[locale]/blog/[slug]">) {
  const { locale, slug } = await params;
  if (!isBlogLanguage(locale)) notFound();

  const post = await fetchBlogPost(locale, slug);
  if (!post) notFound();

  const [tSection, comments] = await Promise.all([getTranslations("metadata.pages"), fetchBlogComments(post.id, locale)]);
  const path = blogPostPath(post.slug);
  const url = `${SITE_URL}/${locale}${path}`;
  const image = post.coverImageUrl ? `${SITE_URL}${post.coverImageUrl}` : `${SITE_URL}${ogImagePath(locale, post.title, tSection("blog.title"))}`;

  return (
    <>
      <JsonLd
        data={jsonLdGraph(
          organizationJsonLd(),
          breadcrumbJsonLd(locale, [
            { name: SITE_NAME, path: "" },
            { name: tSection("blog.title"), path: BLOG_PATH },
            { name: post.title, path },
          ]),
          articleJsonLd({
            locale,
            path,
            headline: post.title,
            description: post.excerpt || post.title,
            datePublished: post.publishedAt,
            dateModified: post.updatedAt,
            image,
          }),
        )}
      />
      <BlogArticle post={post} url={url} comments={comments} renderedAt={new Date().toISOString()} />
    </>
  );
}
