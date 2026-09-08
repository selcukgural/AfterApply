import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { BenchmarkForm } from "@/components/benchmark/BenchmarkForm";

/**
 * The public reply-rate benchmark — the one page on this site that answers a question the product
 * itself cannot yet answer for a stranger: *is my reply rate normal?*
 *
 * It is deliberately answerable without an account. Everything else worth having here lives behind
 * a sign-up, which is the reason nobody has ever seen it; this page hands over its whole value
 * first and mentions the product afterwards.
 *
 * The path is a single shared segment rather than a translated one (/tr/benchmark and
 * /en/benchmark), unlike the guide articles. Localising it would mean adding `pathnames` to
 * next-intl's routing config, which retypes `Link`'s href across the entire app for one page. The
 * search terms are carried by the title, description and heading instead, which is where they do
 * most of their work anyway.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/benchmark">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/benchmark", "benchmark");
}

export default async function BenchmarkPage() {
  const t = await getTranslations("benchmark");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
          {t("title")}
        </h1>
        <p className="max-w-[60ch] text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      <BenchmarkForm />

      {/* Below the form rather than above it: it is what someone reads when they have seen a number
          and are deciding whether to believe it. Every claim here is one the implementation
          actually keeps — see BenchmarkSubmission and BenchmarkCalculations. */}
      <section className="flex flex-col gap-3 border-t border-gray-200 pt-8 dark:border-gray-800">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("method.title")}</h2>
        <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
          <li>{t("method.source")}</li>
          <li>{t("method.median")}</li>
          <li>{t("method.recency")}</li>
          <li>{t("method.privacy")}</li>
        </ul>
      </section>
    </div>
  );
}
