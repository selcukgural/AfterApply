import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { GifFigure } from "@/components/help/GifFigure";
import { Callout } from "@/components/help/Callout";
import { LinkedInArchiveDiagram } from "@/components/imports/LinkedInArchiveDiagram";
import { archiveDiagramLabels } from "@/components/imports/archiveDiagramLabels";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/import">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/import", "helpImport");
}

export default async function ImportHelpPage() {
  const t = await getTranslations("help.import");
  const tCommon = await getTranslations("help.common");
  const tDiagram = await getTranslations("imports.diagram");

  const steps = ["step1", "step2", "step3", "step4", "step5", "step6"].map((key) => ({
    title: t(`steps.${key}.title`),
    body: t(`steps.${key}.body`),
    // The archive-request screen is the only step that happens outside our UI, so it is the only
    // one a screenshot of our app cannot show — the sketch goes here, where the choice is made.
    visual: key === "step1" ? <LinkedInArchiveDiagram labels={archiveDiagramLabels(tDiagram)} /> : undefined,
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("steps.title")}</h2>
        <StepList steps={steps} />
        <GifFigure src="/help/gifs/linkedin-import.gif" alt={t("steps.title")} />
      </section>

      <Callout variant="warning" label={tCommon("warning")} title={t("calloutUnzip.title")}>
        {t("calloutUnzip.body")}
      </Callout>

      <Callout variant="warning" label={tCommon("warning")} title={t("calloutCsv.title")}>
        {t("calloutCsv.body")}
      </Callout>
    </div>
  );
}
