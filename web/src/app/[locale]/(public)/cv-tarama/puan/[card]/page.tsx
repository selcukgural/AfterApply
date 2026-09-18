import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { routing } from "@/i18n/routing";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";
import { CV_SCAN_PATHS, cvScanScorePath } from "@/lib/cvScan/path";
import { parseScoreCard } from "@/lib/cvScan/scoreCard";
import { CvScanForm } from "@/components/cvScan/CvScanForm";
import { CvScanExplainer } from "@/components/cvScan/CvScanExplainer";
import { CvScanCategoryBars } from "@/components/cvScan/CvScanResultCards";

/**
 * Where a shared score lands: `/tr/cv-tarama/puan/88-34-22-13-19` (`/en/cv-scan/score/…` by
 * rewrite). The scan page with one difference — it opens with the number someone passed on, and
 * the upload form is the answer to the question that number asks. Everything under the form is
 * the scan page's own explainer, unchanged.
 *
 * The card in the URL is numbers only (`lib/cvScan/scoreCard.ts`); a card the scan could not have
 * produced is a 404, decided in proxy.ts — not with notFound() here, which would turn a prerendered
 * page dynamic at runtime and 500 in production. The 101 score-only pages are prerendered; a card
 * with subtotals is rendered on first request and cached — there are more of them than are worth
 * building ahead.
 *
 * `canonical` is the scan page itself, so a hundred addresses for one tool do not read as a
 * hundred pages to a search engine; the share card (`/og?card=`) is the only thing here that
 * is per-score.
 */
export function generateStaticParams() {
  return Array.from({ length: 101 }, (_, score) => ({ card: String(score) }));
}

export async function generateMetadata({ params }: PageProps<"/[locale]/cv-tarama/puan/[card]">): Promise<Metadata> {
  const { locale, card: segment } = await params;
  const card = parseScoreCard(segment);
  if (!card) return {};

  const t = await getTranslations({ locale, namespace: "cvScan" });
  const metadata = buildMetadata({
    locale,
    path: CV_SCAN_PATHS,
    title: t("shared.metaTitle", { score: card.score }),
    description: t("shared.metaDescription", { score: card.score }),
  });

  const image = { url: `/${locale}/og?card=${segment}`, width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT, alt: t("shared.metaTitle", { score: card.score }) };
  return {
    ...metadata,
    alternates: {
      ...metadata.alternates,
      // The page's own address in each language, so the share link stays on the score page when
      // a reader switches language; the canonical above stays the tool.
      languages: Object.fromEntries(routing.locales.map((other) => [other, `/${other}${cvScanScorePath(other, segment)}`])),
    },
    openGraph: { ...metadata.openGraph, url: `/${locale}${cvScanScorePath(locale, segment)}`, images: [image] },
    twitter: { ...metadata.twitter, images: [image] },
  };
}

export default async function SharedScorePage({ params }: PageProps<"/[locale]/cv-tarama/puan/[card]">) {
  const { locale, card: segment } = await params;
  setRequestLocale(locale);

  // The proxy has already turned an invalid card into a 404; this is the type narrowing only.
  const card = parseScoreCard(segment);
  if (!card) return null;

  const t = await getTranslations("cvScan");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-6">
        <section className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50 p-6 dark:border-gray-800 dark:bg-gray-900">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("shared.eyebrow")}</p>
          <p className="flex items-baseline gap-2">
            {/* One colour whatever the band, like the share card: this is someone else's number. */}
            <span className="text-5xl font-semibold tracking-tight tabular-nums text-gray-900 dark:text-gray-100">{card.score}</span>
            <span className="text-sm text-gray-500 dark:text-gray-400">{t("shared.outOf")}</span>
          </p>
          <p className="text-sm text-gray-700 dark:text-gray-300">{t("shared.body", { score: card.score })}</p>
          {card.categories ? <CvScanCategoryBars categories={card.categories} /> : null}
        </section>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
          {t("shared.heading")}
        </h1>
      </header>

      <CvScanForm />

      <CvScanExplainer />
    </div>
  );
}
