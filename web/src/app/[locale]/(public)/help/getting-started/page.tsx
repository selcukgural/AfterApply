import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";

export default async function GettingStartedPage() {
  const t = await getTranslations("help.gettingStarted");
  const tCommon = await getTranslations("help.common");

  const registerSteps = ["step1", "step2", "step3", "step4"].map((key) => ({
    title: t(`register.${key}.title`),
    body: t(`register.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("register.title")}</h2>
        <StepList steps={registerSteps} />
        <Screenshot src="/help/screenshots/register-form.png" alt={t("register.title")} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("login.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("login.body")}</p>
        <Screenshot src="/help/screenshots/login-form.png" alt={t("login.title")} />
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutTheme.title")}>
        {t("calloutTheme.body")}
      </Callout>
    </div>
  );
}
