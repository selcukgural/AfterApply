import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/cv">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/cv", "helpCv");
}

export default async function CvHelpPage() {
  const t = await getTranslations("help.cv");
  const tCommon = await getTranslations("help.common");

  const uploadSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`upload.${key}.title`),
    body: t(`upload.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/cv" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <Screenshot src="/help/screenshots/cv-list.png" alt={t("title")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("upload.title")}</h2>
        <StepList steps={uploadSteps} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("preview.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("preview.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("default.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("default.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("remove.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("remove.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutPrivacy.title")}>
        {t("calloutPrivacy.body")}
      </Callout>
    </div>
  );
}
