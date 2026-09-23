import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { OFFER_COMPARE_PATHS } from "@/lib/offerCompare/path";
import { OfferComparison } from "@/components/offerCompare/OfferComparison";
import { SalaryCta } from "@/components/offerCompare/SalaryCta";
import { Link } from "@/i18n/navigation";

/** The texts the figures come from — the same ones `lib/offerCompare/payroll.ts` cites. */
const SOURCES = [
  { key: "sourceTariff", href: "https://www.resmigazete.gov.tr/eskiler/2025/12/20251231M5-30.pdf" },
  {
    key: "sourceGuide",
    href: "https://cdn.gib.gov.tr/api/gibportal-file/file/getFileResources?objectKey=arsiv%2Ffileadmin%2Fbeyannamerehberi%2F2026%2F2026_Ucret_Geliri.pdf",
  },
  {
    key: "sourceSgk",
    href: "https://www.sgk.gov.tr/Content/Post/2e0c9e1a-2cfe-4456-af10-49d3de0c58ba/Prime-Esas-Kazanc-Miktarlari-2026-01-14-10-35-39",
  },
] as const;

/** Gross → net in the order the payroll does it — `lib/offerCompare/netSalary.ts`, in words. */
const STEPS = ["sgk", "base", "exemption", "stamp", "net"] as const;

const METHOD_LINES = ["brackets", "minimumWage", "sgk", "benefits", "netOffer", "assumptions", "updated"] as const;

/**
 * The job-offer comparison (competitor research item 6, design canvas variant B, 2026-09-24):
 * two or three offers over a calendar year, gross→net under the year's payroll rules, entirely in
 * the browser. Public and sign-up-free like the benchmark and the CV scan; it ends on the one
 * thing the product wants back from someone holding an offer — their salary, anonymously.
 *
 * The route directory is the Turkish slug and `/en/offer-comparison` is a rewrite onto it, the
 * way the CV scan is done (`lib/offerCompare/path.ts`).
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/teklif-karsilastirma">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, OFFER_COMPARE_PATHS, "offerCompare");
}

export default async function OfferComparePage({ params }: PageProps<"/[locale]/teklif-karsilastirma">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("offerCompare");

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <p className="text-xs font-semibold tracking-wider text-accent uppercase">{t("eyebrow")}</p>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-[62ch] text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </header>

      <OfferComparison />

      <div className="grid items-start gap-6 md:grid-cols-2">
        <SalaryCta />
        {/* The verdict's "see how we calculate" lands here. Everything the page claims about its own
            arithmetic is in this section — the steps, the figures, the assumptions and what it
            leaves out — so that nobody mistakes the result for a payslip (2026-09-24). */}
        <section
          id="method"
          className="flex scroll-mt-24 flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900"
        >
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("method.title")}</h2>
          <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("method.stepsTitle")}</h3>
          <ol className="flex list-decimal flex-col gap-2 pl-5 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {STEPS.map((step) => (
              <li key={step}>{t(`method.steps.${step}`)}</li>
            ))}
          </ol>
          <h3 className="pt-2 text-sm font-semibold text-gray-900 dark:text-gray-100">{t("method.rulesTitle")}</h3>
          <ul className="flex list-disc flex-col gap-2 pl-5 text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {METHOD_LINES.map((line) => (
              <li key={line}>{t(`method.${line}`)}</li>
            ))}
          </ul>
          <h3 className="pt-2 text-sm font-semibold text-gray-900 dark:text-gray-100">{t("method.sourcesTitle")}</h3>
          <ul className="flex flex-col gap-1.5 text-sm">
            {SOURCES.map((source) => (
              <li key={source.key}>
                <a href={source.href} target="_blank" rel="noopener noreferrer" className="text-accent-ink hover:underline">
                  {t(`method.${source.key}`)}
                </a>
              </li>
            ))}
          </ul>
          <Link href="/help/offer-comparison" className="text-sm font-medium text-accent-ink hover:underline">
            {t("method.helpLink")}
          </Link>
          <p className="mt-2 rounded-md bg-gray-50 p-3 text-sm leading-relaxed text-gray-700 dark:bg-gray-800/60 dark:text-gray-300">
            {t.rich("method.disclaimer", {
              terms: (chunks) => (
                <Link href="/terms#liability" className="font-medium text-accent-ink hover:underline">
                  {chunks}
                </Link>
              ),
            })}
          </p>
        </section>
      </div>
    </div>
  );
}
