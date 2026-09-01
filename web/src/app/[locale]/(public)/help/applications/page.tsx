import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { GifFigure } from "@/components/help/GifFigure";
import { Callout } from "@/components/help/Callout";

export default async function ApplicationsHelpPage() {
  const t = await getTranslations("help.applications");
  const tCommon = await getTranslations("help.common");

  const createSteps = ["step1", "step2", "step3", "step4"].map((key) => ({
    title: t(`create.${key}.title`),
    body: t.has(`create.${key}.body`) ? t(`create.${key}.body`) : undefined,
  }));
  const statusSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`statusChange.${key}.title`),
    body: t.has(`statusChange.${key}.body`) ? t(`statusChange.${key}.body`) : undefined,
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("create.title")}</h2>
        <StepList steps={createSteps} />
        <Screenshot src="/help/screenshots/application-create.png" alt={t("create.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("list.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("list.body")}</p>
        <Screenshot src="/help/screenshots/applications-list.png" alt={t("list.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("detail.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("detail.body")}</p>
        <Screenshot src="/help/screenshots/application-detail.png" alt={t("detail.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("edit.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("edit.body")}</p>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("delete.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("delete.body")}</p>
        <Callout variant="danger" label={tCommon("warning")} title={t("delete.calloutConfirm.title")}>
          {t("delete.calloutConfirm.body")}
        </Callout>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("statusChange.title")}</h2>
        <StepList steps={statusSteps} />
        <GifFigure src="/help/gifs/application-status-change.gif" alt={t("statusChange.title")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("statuses.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("statuses.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("timeline.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("timeline.body")}</p>
      </section>
    </div>
  );
}
