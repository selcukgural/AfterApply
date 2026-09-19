import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import type { BlogLanguage } from "@/types/api";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { BLOG_PATH, blogPostPath } from "@/lib/blog/blogPaths";
import { fetchBlogList } from "@/lib/blog/publicApi.server";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, jsonLdGraph, organizationJsonLd } from "@/lib/seo/jsonLd";
import { SITE_NAME } from "@/lib/seo/routes";

function isBlogLanguage(locale: string): locale is BlogLanguage {
  return (routing.locales as readonly string[]).includes(locale);
}

export async function generateMetadata({ params }: PageProps<"/[locale]/blog">): Promise<Metadata> {
  const { locale } = await params;
  setRequestLocale(locale);
  return pageMetadata(locale, BLOG_PATH, "blog");
}

/**
 * The blog's front page: the published posts of this locale's language, newest first. Rendered on
 * the server from a `no-store` fetch, so the page is dynamic and `notFound()` is safe here (the
 * static-page 500 of 2026-09-16 does not apply). A blog with nothing published in this language
 * is not a page at all — the 404 is the rule, and the "Blog" links follow the same flag.
 */
export default async function BlogListPage({ params, searchParams }: PageProps<"/[locale]/blog">) {
  const { locale } = await params;
  if (!isBlogLanguage(locale)) notFound();
  const { page: rawPage } = await searchParams;
  const page = Math.max(1, Number.parseInt(typeof rawPage === "string" ? rawPage : "1", 10) || 1);

  const list = await fetchBlogList(locale, page);
  if (!list || list.totalCount === 0) notFound();
  const totalPages = Math.max(1, Math.ceil(list.totalCount / list.pageSize));
  if (page > totalPages) notFound();

  const t = await getTranslations("blog");
  const tSection = await getTranslations("metadata.pages");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <JsonLd
        data={jsonLdGraph(
          organizationJsonLd(),
          breadcrumbJsonLd(locale, [
            { name: SITE_NAME, path: "" },
            { name: tSection("blog.title"), path: BLOG_PATH },
          ]),
        )}
      />

      <header className="flex flex-col gap-2">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-lg leading-7 text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      <ul className="flex flex-col divide-y divide-gray-200 dark:divide-gray-800">
        {list.items.map((post) => (
          <li key={post.id} className="py-6 first:pt-0">
            <article className="flex flex-col gap-2 sm:flex-row sm:gap-6">
              {post.coverImageUrl && (
                <Link href={blogPostPath(post.slug)} className="shrink-0 sm:w-40" tabIndex={-1} aria-hidden="true">
                  {/* Same-origin path (next.config rewrites it to the API); plain <img> like the
                      rest of the public site. */}
                  <img
                    src={post.coverImageUrl}
                    alt=""
                    loading="lazy"
                    className="aspect-[16/10] w-full rounded-lg border border-gray-200 object-cover dark:border-gray-800"
                  />
                </Link>
              )}
              <div className="flex min-w-0 flex-col gap-2">
                <time dateTime={post.publishedAt} className="text-xs text-gray-500 dark:text-gray-500">
                  {formatArticleDate(post.publishedAt.slice(0, 10), locale)}
                </time>
                <h2 className="text-xl font-semibold text-gray-900 dark:text-gray-100">
                  <Link href={blogPostPath(post.slug)} className="hover:underline underline-offset-2">
                    {post.title}
                  </Link>
                </h2>
                {post.excerpt && <p className="leading-7 text-gray-700 dark:text-gray-300">{post.excerpt}</p>}
                <div className="flex items-center gap-4 text-sm">
                  <Link href={blogPostPath(post.slug)} className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
                    {t("readMore")}
                  </Link>
                  {post.likeCount > 0 && <span className="text-xs text-gray-500 dark:text-gray-400">{t("like", { count: post.likeCount })}</span>}
                </div>
              </div>
            </article>
          </li>
        ))}
      </ul>

      {totalPages > 1 && (
        <nav aria-label={t("pagination")} className="flex items-center justify-between text-sm text-gray-600 dark:text-gray-400">
          <span>{t("pageInfo", { page, totalPages })}</span>
          <div className="flex gap-3">
            {page > 1 && (
              <Link href={page === 2 ? BLOG_PATH : `${BLOG_PATH}?page=${page - 1}`} className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
                {t("newer")}
              </Link>
            )}
            {page < totalPages && (
              <Link href={`${BLOG_PATH}?page=${page + 1}`} className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
                {t("older")}
              </Link>
            )}
          </div>
        </nav>
      )}
    </div>
  );
}
