import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { GifFigure } from "@/components/help/GifFigure";
import { Callout } from "@/components/help/Callout";

export default async function ImportHelpPage() {
  const t = await getTranslations("help.import");
  const tCommon = await getTranslations("help.common");

  const steps = ["step1", "step2", "step3", "step4", "step5"].map((key) => ({
    title: t(`steps.${key}.title`),
    body: t(`steps.${key}.body`),
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

      <Callout variant="warning" label={tCommon("warning")} title={t("calloutCsv.title")}>
        {t("calloutCsv.body")}
      </Callout>
    </div>
  );
}
