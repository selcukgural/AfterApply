import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";

export default async function SuggestionsHelpPage() {
  const t = await getTranslations("help.suggestions");
  const tCommon = await getTranslations("help.common");

  const actionSteps = ["step1", "step2"].map((key) => ({
    title: t(`actions.${key}.title`),
    body: t(`actions.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <Screenshot src="/help/screenshots/suggestions-list.png" alt={t("title")} />

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("how.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("how.body")}</p>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("actions.title")}</h2>
        <StepList steps={actionSteps} />
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutConfidence.title")}>
        {t("calloutConfidence.body")}
      </Callout>
    </div>
  );
}
