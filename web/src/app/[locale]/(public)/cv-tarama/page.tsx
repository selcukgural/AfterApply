import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { CvScanForm } from "@/components/cvScan/CvScanForm";
import { CvScanExplainer } from "@/components/cvScan/CvScanExplainer";
import { CV_SCAN_PATHS } from "@/lib/cvScan/path";

/**
 * The sign-up-free CV scan — the lower of the two public doorsteps. The benchmark asks a stranger
 * to type in their own numbers; this one lets them arrive with a file they already have.
 *
 * **The order of this page is a decision, not a layout accident: the upload comes first and the
 * explanation second.** Someone who already knows what an ATS is should not have to scroll past a
 * paragraph explaining it, and the page's whole promise is an answer rather than an argument. The
 * cost is that a reader can upload before reading the correction to the "75% auto-reject" myth —
 * paid for by the same correction sitting next to the score on the result screen, where it cannot
 * be scrolled past.
 *
 * The route directory is the Turkish slug — "cv tarama" is the phrase the audience this page is
 * built for actually searches — and the English page lives at /en/cv-scan by rewrite (see
 * src/lib/cvScan/path.ts). Localising it through next-intl's `pathnames` would retype `Link`'s
 * href across the whole app, so it is done the way the guide slugs are.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/cv-tarama">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, CV_SCAN_PATHS, "cvScan");
}

export default async function CvScanPage({ params }: PageProps<"/[locale]/cv-tarama">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("cvScan");

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
          {t("title")}
        </h1>
        <p className="max-w-[60ch] text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      <CvScanForm />

      <CvScanExplainer />
    </div>
  );
}
