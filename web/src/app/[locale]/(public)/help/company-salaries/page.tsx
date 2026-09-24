import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Callout } from "@/components/help/Callout";
import { Screenshot } from "@/components/help/Screenshot";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/company-salaries">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/company-salaries", "helpCompanySalaries");
}

export default async function CompanySalariesHelpPage({ params }: PageProps<"/[locale]/help/company-salaries">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("help.companySalaries");
  const tCommon = await getTranslations("help.common");

  const readSteps = ["step1", "step2", "step3"].map((key) => ({ title: t(`read.${key}.title`), body: t(`read.${key}.body`) }));
  const writeSteps = ["step1", "step2", "step3", "step4"].map((key) => ({ title: t(`write.${key}.title`), body: t(`write.${key}.body`) }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/company-salaries" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("read.title")}</h2>
        <StepList steps={readSteps} />
        <Screenshot src="/help/screenshots/company-salaries.png" alt={t("read.title")} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("write.title")}</h2>
        <StepList steps={writeSteps} />
        <Screenshot src="/help/screenshots/salary-form.png" alt={t("write.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("position.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("position.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutAnonymous.title")}>
        {t("calloutAnonymous.body")}
      </Callout>

      <Callout variant="info" label={tCommon("note")} title={t("calloutLimits.title")}>
        {t("calloutLimits.body")}
      </Callout>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("manage.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("manage.body")}</p>
      </section>
    </div>
  );
}
