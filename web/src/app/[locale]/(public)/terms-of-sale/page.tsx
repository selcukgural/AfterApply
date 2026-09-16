import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";

// The distance-sales agreement (Mesafeli Satış Sözleşmesi) and the pre-contract information form
// for the Pro plan, as 6502 sayılı Kanun and the Mesafeli Sözleşmeler Yönetmeliği require for a
// consumer sale; PayTR's merchant review also looks for this page. The section structure is
// fixed here; the text of each section lives in the message catalogues (final since 2026-09-16:
// seven-day no-questions refund, used time deducted after that — DECISIONS.md).
export async function generateMetadata({ params }: PageProps<"/[locale]/terms-of-sale">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/terms-of-sale", "termsOfSale");
}

const SECTIONS = ["parties", "subject", "service", "price", "payment", "delivery", "withdrawal", "refunds", "obligations", "disputes", "effective"] as const;

export default async function TermsOfSalePage({ params }: PageProps<"/[locale]/terms-of-sale">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("termsOfSale");

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="mt-8 flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <p>{t("intro")}</p>
        {SECTIONS.map((section, index) => (
          <section key={section} id={section}>
            <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">
              {index + 1}. {t(`sections.${section}.title`)}
            </h2>
            <p className="whitespace-pre-line">{t(`sections.${section}.body`)}</p>
          </section>
        ))}
        <p>
          {t("seeAlso")}{" "}
          <Link href="/refund-policy" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("refundLink")}
          </Link>
          {" · "}
          <Link href="/privacy#payments" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("privacyLink")}
          </Link>
        </p>
      </div>
    </div>
  );
}
