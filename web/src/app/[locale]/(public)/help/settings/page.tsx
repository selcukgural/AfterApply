import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";

export default async function SettingsHelpPage() {
  const t = await getTranslations("help.settings");
  const tCommon = await getTranslations("help.common");

  const extensionSteps = ["step1", "step2", "step3", "step4"].map((key) => ({
    title: t(`extension.${key}.title`),
    body: t(`extension.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("export.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("export.body")}</p>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("extension.title")}</h2>
        <StepList steps={extensionSteps} />
        <Screenshot src="/help/screenshots/settings-extension-token.png" alt={t("extension.title")} />
        <Callout variant="danger" label={tCommon("warning")} title={t("extension.calloutToken.title")}>
          {t("extension.calloutToken.body")}
        </Callout>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("emailForwarding.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("emailForwarding.body")}</p>
        <Screenshot src="/help/screenshots/settings-email-forwarding.png" alt={t("emailForwarding.title")} />
        <Callout variant="info" label={tCommon("note")} title={t("emailForwarding.calloutGmailOnlyGuide.title")}>
          {t("emailForwarding.calloutGmailOnlyGuide.body")}
        </Callout>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("delete.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("delete.body")}</p>
        <Screenshot src="/help/screenshots/settings-delete-account.png" alt={t("delete.title")} />
        <Callout variant="danger" label={tCommon("warning")} title={t("delete.calloutIrreversible.title")}>
          {t("delete.calloutIrreversible.body")}
        </Callout>
      </section>
    </div>
  );
}
