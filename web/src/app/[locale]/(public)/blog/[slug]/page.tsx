import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import type { BlogLanguage } from "@/types/api";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { SITE_NAME, SITE_URL } from "@/lib/seo/routes";
import { ogImagePath } from "@/lib/seo/ogImage";
import { JsonLd } from "@/components/seo/JsonLd";
import { articleJsonLd, breadcrumbJsonLd, jsonLdGraph, organizationJsonLd } from "@/lib/seo/jsonLd";
import { BLOG_PATH, blogAlternates, blogPostPath } from "@/lib/blog/blogPaths";
import { fetchBlogPost } from "@/lib/blog/publicApi.server";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";
import { ShareRow } from "@/components/share/ShareRow";
import { BlogArticleBody } from "@/components/blog/BlogArticleBody";
import { LikeButton } from "@/components/blog/LikeButton";

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
 * here. The body is the API's sanitized HTML (see BlogArticleBody); everything else on the page is
 * ordinary data.
 */
export default async function BlogPostPage({ params }: PageProps<"/[locale]/blog/[slug]">) {
  const { locale, slug } = await params;
  if (!isBlogLanguage(locale)) notFound();

  const post = await fetchBlogPost(locale, slug);
  if (!post) notFound();

  const t = await getTranslations("blog");
  const tSection = await getTranslations("metadata.pages");
  const path = blogPostPath(post.slug);
  const url = `${SITE_URL}/${locale}${path}`;
  const image = post.coverImageUrl ? `${SITE_URL}${post.coverImageUrl}` : `${SITE_URL}${ogImagePath(locale, post.title, tSection("blog.title"))}`;
  const updated = post.updatedAt.slice(0, 10) !== post.publishedAt.slice(0, 10);

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
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

      <header className="flex flex-col gap-3">
        <Link href={BLOG_PATH} className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {tSection("blog.title")}
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{post.title}</h1>
        {post.excerpt && <p className="text-lg leading-7 text-gray-600 dark:text-gray-400">{post.excerpt}</p>}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500 dark:text-gray-500">
          <time dateTime={post.publishedAt}>{t("publishedOn", { date: formatArticleDate(post.publishedAt.slice(0, 10), locale) })}</time>
          {updated && (
            <time dateTime={post.updatedAt}>{t("updatedOn", { date: formatArticleDate(post.updatedAt.slice(0, 10), locale) })}</time>
          )}
          {post.translation && (
            <Link
              href={blogPostPath(post.translation.slug)}
              locale={post.translation.language}
              hrefLang={post.translation.language}
              className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400"
            >
              {t("readInOtherLanguage", { language: post.translation.language })}
            </Link>
          )}
        </div>
        {post.coverImageUrl && (
          <img
            src={post.coverImageUrl}
            alt=""
            className="mt-2 w-full rounded-lg border border-gray-200 object-cover dark:border-gray-800"
          />
        )}
      </header>

      <div className="border-t border-gray-200 pt-6 dark:border-gray-800">
        <BlogArticleBody html={post.contentHtml} lang={post.language} />
      </div>

      <footer className="flex flex-col gap-4 border-t border-gray-200 pt-6 dark:border-gray-800">
        <div className="flex flex-wrap items-center gap-4">
          <LikeButton
            postId={post.id}
            initialCount={post.likeCount}
            initialLiked={post.likedByMe}
            signInHref={`/login?next=${encodeURIComponent(path)}`}
          />
          <ShareRow label={t("share")} content={{ text: post.title, url }} />
        </div>
        <Link href={BLOG_PATH} className="text-sm font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
          {t("backToList")}
        </Link>
      </footer>
    </div>
  );
}
