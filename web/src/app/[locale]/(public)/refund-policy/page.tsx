import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";

// Cancellation and refund terms for the Pro plan. The mechanics are fixed by the product (a
// request from the app or by e-mail, an admin's decision, the money back through PayTR to the
// same card, the Pro period wound back in proportion); the policy — full refund within seven
// days, used time deducted after that, a reply within three business days — is the owner's
// (2026-09-16) and PaymentOrder.PolicyRefundMinor computes the same rule for the admin panel.
export async function generateMetadata({ params }: PageProps<"/[locale]/refund-policy">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/refund-policy", "refundPolicy");
}

const SECTIONS = ["scope", "withdrawal", "cancellation", "howToRequest", "decision", "howRefunded", "partial", "proAfterRefund", "contact"] as const;

export default async function RefundPolicyPage({ params }: PageProps<"/[locale]/refund-policy">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("refundPolicy");

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="mt-8 flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <p>{t("intro")}</p>
        {SECTIONS.map((section) => (
          <section key={section} id={section}>
            <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t(`sections.${section}.title`)}</h2>
            <p className="whitespace-pre-line">{t(`sections.${section}.body`)}</p>
          </section>
        ))}
        <p>
          {t("seeAlso")}{" "}
          <Link href="/terms-of-sale" className="text-blue-600 hover:underline dark:text-blue-400">
            {t("termsLink")}
          </Link>
        </p>
      </div>
    </div>
  );
}
