import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { GifFigure } from "@/components/help/GifFigure";
import { Callout } from "@/components/help/Callout";

export default async function TrackedJobsHelpPage() {
  const t = await getTranslations("help.trackedJobs");
  const tCommon = await getTranslations("help.common");

  const addSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`add.${key}.title`),
    body: t(`add.${key}.body`),
  }));
  const convertSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`convert.${key}.title`),
    body: t(`convert.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <Screenshot src="/help/screenshots/tracked-jobs-list.png" alt={t("title")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("add.title")}</h2>
        <StepList steps={addSteps} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("convert.title")}</h2>
        <StepList steps={convertSteps} />
        <GifFigure src="/help/gifs/tracked-job-convert.gif" alt={t("convert.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("remove.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("remove.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutNoEdit.title")}>
        {t("calloutNoEdit.body")}
      </Callout>
    </div>
  );
}
