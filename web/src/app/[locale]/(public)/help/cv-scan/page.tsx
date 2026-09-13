import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { StepList } from "@/components/help/StepList";
import { Callout } from "@/components/help/Callout";
import { Screenshot } from "@/components/help/Screenshot";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/cv-scan">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/cv-scan", "helpCvScan");
}

export default async function CvScanHelpPage() {
  const t = await getTranslations("help.cvScan");
  const tCommon = await getTranslations("help.common");

  const steps = ["step1", "step2", "step3"].map((key) => ({ title: t(`how.${key}.title`), body: t(`how.${key}.body`) }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/cv-scan" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
        <Link href="/cv-tarama" className="text-sm font-medium text-accent-ink hover:underline">
          {t("openTool")}
        </Link>
      </div>

      <Screenshot src="/help/screenshots/cv-scan-result.png" alt={t("title")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("how.title")}</h2>
        <StepList steps={steps} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("measures.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("measures.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("limits.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("limits.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutPrivacy.title")}>
        {t("calloutPrivacy.body")}
      </Callout>

      <Callout variant="warning" label={tCommon("warning")} title={t("calloutRateLimit.title")}>
        {t("calloutRateLimit.body")}
      </Callout>
    </div>
  );
}
