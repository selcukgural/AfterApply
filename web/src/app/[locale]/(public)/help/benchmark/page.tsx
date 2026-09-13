import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { StepList } from "@/components/help/StepList";
import { Callout } from "@/components/help/Callout";
import { Screenshot } from "@/components/help/Screenshot";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/benchmark">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/benchmark", "helpBenchmark");
}

export default async function BenchmarkHelpPage() {
  const t = await getTranslations("help.benchmark");
  const tCommon = await getTranslations("help.common");

  const steps = ["step1", "step2", "step3"].map((key) => ({ title: t(`how.${key}.title`), body: t(`how.${key}.body`) }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/benchmark" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
        <Link href="/benchmark" className="text-sm font-medium text-accent-ink hover:underline">
          {t("openTool")}
        </Link>
      </div>

      <Screenshot src="/help/screenshots/benchmark-result.png" alt={t("title")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("how.title")}</h2>
        <StepList steps={steps} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("median.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("median.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("scope.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("scope.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutAnonymous.title")}>
        {t("calloutAnonymous.body")}
      </Callout>

      <Callout variant="info" label={tCommon("note")} title={t("calloutSignedIn.title")}>
        {t("calloutSignedIn.body")}
      </Callout>
    </div>
  );
}
