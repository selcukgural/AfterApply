import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, jsonLdGraph } from "@/lib/seo/jsonLd";
import { GUIDE_PATH, isGuideLocale } from "@/lib/guide/guideLinks";
import { postPath } from "@/lib/blog/blogPaths";
import { fetchGuideList } from "@/lib/blog/publicApi.server";
import { SITE_NAME } from "@/lib/seo/routes";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";

export async function generateMetadata({ params }: PageProps<"/[locale]/guide">): Promise<Metadata> {
  const { locale } = await params;
  setRequestLocale(locale);
  return pageMetadata(locale, GUIDE_PATH, "guide");
}

/**
 * The guide's index: this language's published guides, newest first, as the same flat list of
 * title, summary and date it has always been. From the API since 2026-09-26, rendered per request
 * (`no-store`), so the page is dynamic and `notFound()` is safe here. Paged the way the blog is,
 * once there are more guides than one page holds.
 */
export default async function GuideIndexPage({ params, searchParams }: PageProps<"/[locale]/guide">) {
  const { locale } = await params;
  if (!isGuideLocale(locale)) notFound();
  const { page: rawPage } = await searchParams;
  const page = Math.max(1, Number.parseInt(typeof rawPage === "string" ? rawPage : "1", 10) || 1);

  const list = await fetchGuideList(locale, page);
  if (!list) notFound();

  const totalPages = Math.max(1, Math.ceil(list.totalCount / list.pageSize));
  if (page > totalPages) notFound();

  const t = await getTranslations("guide.index");
  const tBlog = await getTranslations("blog");
  const tSection = await getTranslations("metadata.pages");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <JsonLd
        data={jsonLdGraph(
          breadcrumbJsonLd(locale, [
            { name: SITE_NAME, path: "" },
            { name: tSection("guide.title"), path: GUIDE_PATH },
          ]),
        )}
      />

      <header className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </header>

      <div className="flex flex-col divide-y divide-gray-200 dark:divide-gray-800">
        {list.items.map((guide) => (
          <article key={guide.id} className="flex flex-col gap-3 py-6 first:pt-0 sm:flex-row sm:gap-6">
            {/* A guide with a cover shows it the way a blog card does (2026-09-26); one without
                keeps the plain row the guide has always had. */}
            {guide.coverImageUrl && (
              <Link href={postPath("Guide", guide.slug)} className="shrink-0 sm:w-40" tabIndex={-1} aria-hidden="true">
                {/* Same-origin path (next.config rewrites it to the API); plain <img> like the blog list. */}
                <img
                  src={guide.coverImageUrl}
                  alt=""
                  loading="lazy"
                  className="aspect-[16/10] w-full rounded-lg border border-gray-200 object-cover dark:border-gray-800"
                />
              </Link>
            )}
            <div className="flex min-w-0 flex-col gap-2">
              <h2 className="text-lg font-medium">
                <Link
                  href={postPath("Guide", guide.slug)}
                  className="text-gray-900 hover:text-blue-700 dark:text-gray-100 dark:hover:text-blue-400"
                >
                  {guide.title}
                </Link>
              </h2>
              {guide.excerpt && <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{guide.excerpt}</p>}
              <time dateTime={guide.updatedAt} className="text-xs text-gray-500 dark:text-gray-500">
                {formatArticleDate(guide.updatedAt.slice(0, 10), locale)}
              </time>
            </div>
          </article>
        ))}
      </div>

      {totalPages > 1 && (
        <nav aria-label={tBlog("pagination")} className="flex items-center justify-between text-sm text-gray-600 dark:text-gray-400">
          <span>{tBlog("pageInfo", { page, totalPages })}</span>
          <div className="flex gap-3">
            {page > 1 && (
              <Link href={page === 2 ? GUIDE_PATH : `${GUIDE_PATH}?page=${page - 1}`} className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
                {tBlog("newer")}
              </Link>
            )}
            {page < totalPages && (
              <Link href={`${GUIDE_PATH}?page=${page + 1}`} className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
                {tBlog("older")}
              </Link>
            )}
          </div>
        </nav>
      )}

      <p className="text-sm text-gray-600 dark:text-gray-400">
        {t.rich("helpLink", {
          link: (chunks) => (
            <Link href="/help" className="font-medium text-blue-600 underline underline-offset-2 dark:text-blue-400">
              {chunks}
            </Link>
          ),
        })}
      </p>
    </div>
  );
}
