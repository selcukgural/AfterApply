import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { StepList } from "@/components/help/StepList";
import { Callout } from "@/components/help/Callout";
import { Screenshot } from "@/components/help/Screenshot";
import { offerComparePath } from "@/lib/offerCompare/path";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/offer-comparison">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/offer-comparison", "helpOfferCompare");
}

/**
 * The offer comparison's help topic (2026-09-24). It explains the two things the tool's own page
 * can only state in a line — why a gross salary nets less as the year goes on, and what a net
 * agreement means — and repeats the page's estimate disclaimer where a reader looking for help
 * will see it.
 */
export default async function OfferCompareHelpPage({ params }: PageProps<"/[locale]/help/offer-comparison">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("help.offerCompare");
  const tCommon = await getTranslations("help.common");

  const steps = ["step1", "step2", "step3"].map((key) => ({ title: t(`how.${key}.title`), body: t(`how.${key}.body`) }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/offer-comparison" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
        <Link href={offerComparePath(locale)} className="text-sm font-medium text-accent-ink hover:underline">
          {t("openTool")}
        </Link>
      </div>

      <Screenshot src="/help/screenshots/offer-comparison.png" alt={t("screenshotAlt")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("how.title")}</h2>
        <StepList steps={steps} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("grossNet.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("grossNet.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("monthly.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("monthly.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("excluded.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("excluded.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutPrivacy.title")}>
        {t("calloutPrivacy.body")}
      </Callout>

      <Callout variant="warning" label={tCommon("note")} title={t("calloutEstimate.title")}>
        {t("calloutEstimate.body")}
      </Callout>
    </div>
  );
}
