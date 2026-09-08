import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { articleJsonLd, breadcrumbJsonLd, jsonLdGraph } from "@/lib/seo/jsonLd";
import {
  GUIDE_ARTICLES,
  GUIDE_PATH,
  articlePath,
  articlePaths,
  findArticleByKey,
  findArticleBySlug,
  isGuideLocale,
} from "@/lib/guide/articles";
import { loadGuideArticle } from "@/lib/guide/content";
import { SITE_NAME } from "@/lib/seo/routes";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";

export function generateStaticParams({ params }: { params: { locale: string } }) {
  if (!isGuideLocale(params.locale)) return [];
  return GUIDE_ARTICLES.map((article) => ({ slug: article.copy[params.locale as "tr" | "en"].slug }));
}

export async function generateMetadata({ params }: PageProps<"/[locale]/guide/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  if (!isGuideLocale(locale)) return {};

  const article = findArticleBySlug(slug, locale);
  if (!article) return {};

  return buildMetadata({
    locale,
    // The slug differs per locale, so the hreflang set has to be built from the article rather
    // than from this page's own path.
    path: articlePaths(article),
    title: article.copy[locale].title,
    description: article.copy[locale].description,
    article: { publishedTime: article.published, modifiedTime: article.updated },
  });
}

export default async function GuideArticlePage({ params }: PageProps<"/[locale]/guide/[slug]">) {
  const { locale, slug } = await params;
  if (!isGuideLocale(locale)) notFound();

  const article = findArticleBySlug(slug, locale);
  if (!article) notFound();

  const copy = article.copy[locale];
  const Body = await loadGuideArticle(article.key, locale);
  const t = await getTranslations("guide.article");
  const tSection = await getTranslations("metadata.pages");

  const related = article.related
    .map((key) => findArticleByKey(key))
    .filter((entry) => entry !== undefined)
    .slice(0, 2);

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <JsonLd
        data={jsonLdGraph(
          breadcrumbJsonLd(locale, [
            { name: SITE_NAME, path: "" },
            { name: tSection("guide.title"), path: GUIDE_PATH },
            { name: copy.title, path: articlePath(article, locale) },
          ]),
          articleJsonLd({
            locale,
            path: articlePath(article, locale),
            headline: copy.title,
            description: copy.description,
            datePublished: article.published,
            dateModified: article.updated,
          }),
        )}
      />

      <header className="flex flex-col gap-3">
        <Link href={GUIDE_PATH} className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {tSection("guide.title")}
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{copy.title}</h1>
        <p className="text-lg leading-7 text-gray-600 dark:text-gray-400">{copy.description}</p>
        <time
          dateTime={article.updated ?? article.published}
          className="text-xs text-gray-500 dark:text-gray-500"
        >
          {article.updated
            ? t("updatedOn", { date: formatArticleDate(article.updated, locale) })
            : t("publishedOn", { date: formatArticleDate(article.published, locale) })}
        </time>
      </header>

      <div className="border-t border-gray-200 pt-2 dark:border-gray-800">
        <Body />
      </div>

      <section className="rounded-lg border border-blue-200 bg-blue-50/60 p-6 dark:border-blue-900 dark:bg-blue-950/30">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("cta.title")}</h2>
        <p className="mt-2 text-sm leading-6 text-gray-700 dark:text-gray-300">{t("cta.body")}</p>
        <Link
          href="/register"
          className="mt-4 inline-flex items-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          {t("cta.button")}
        </Link>
      </section>

      {related.length > 0 && (
        <section className="flex flex-col gap-3 border-t border-gray-200 pt-8 dark:border-gray-800">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("related")}</h2>
          <ul className="flex flex-col gap-2">
            {related.map((entry) => (
              <li key={entry.key}>
                <Link
                  href={articlePath(entry, locale)}
                  className="text-sm font-medium text-blue-600 underline underline-offset-2 dark:text-blue-400"
                >
                  {entry.copy[locale].title}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
