import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, jsonLdGraph } from "@/lib/seo/jsonLd";
import { GUIDE_ARTICLES, GUIDE_PATH, articlePath, isGuideLocale } from "@/lib/guide/articles";
import { SITE_NAME } from "@/lib/seo/routes";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";

export async function generateMetadata({ params }: PageProps<"/[locale]/guide">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, GUIDE_PATH, "guide");
}

export default async function GuideIndexPage({ params }: PageProps<"/[locale]/guide">) {
  const { locale } = await params;
  if (!isGuideLocale(locale)) notFound();

  const t = await getTranslations("guide.index");
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
        {GUIDE_ARTICLES.map((article) => {
          const copy = article.copy[locale];
          return (
            <article key={article.key} className="flex flex-col gap-2 py-6 first:pt-0">
              <h2 className="text-lg font-medium">
                <Link
                  href={articlePath(article, locale)}
                  className="text-gray-900 hover:text-blue-700 dark:text-gray-100 dark:hover:text-blue-400"
                >
                  {copy.title}
                </Link>
              </h2>
              <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{copy.description}</p>
              <time
                dateTime={article.updated ?? article.published}
                className="text-xs text-gray-500 dark:text-gray-500"
              >
                {formatArticleDate(article.updated ?? article.published, locale)}
              </time>
            </article>
          );
        })}
      </div>

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
