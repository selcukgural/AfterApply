import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";
import { buttonClassName } from "@/components/ui/Button";

const CHROME_WEB_STORE_URL =
  "https://chromewebstore.google.com/detail/e-kariyerim-%E2%80%94-job-import/lemdkeljacdgbbcmefpbggphnhhciimi";

export default async function ChromeExtensionHelpPage() {
  const t = await getTranslations("help.chromeExtension");
  const tCommon = await getTranslations("help.common");

  const installSteps = ["step1", "step2"].map((key) => ({
    title: t(`install.${key}.title`),
    body: t.has(`install.${key}.body`) ? t(`install.${key}.body`) : undefined,
  }));
  const installManualSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`installManual.${key}.title`),
    body: t.has(`installManual.${key}.body`) ? t(`installManual.${key}.body`) : undefined,
  }));
  const configureSteps = ["step1", "step2", "step3"].map((key) => ({
    title: t(`configure.${key}.title`),
    body: t.has(`configure.${key}.body`) ? t(`configure.${key}.body`) : undefined,
  }));
  const useSteps = ["step1", "step2", "step3", "step4"].map((key) => ({
    title: t(`use.${key}.title`),
    body: t.has(`use.${key}.body`) ? t(`use.${key}.body`) : undefined,
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("install.title")}</h2>
        <div className="flex flex-col gap-3 rounded-lg border border-blue-100 bg-blue-50 p-4 dark:border-blue-900/50 dark:bg-blue-950/30">
          <p className="text-sm leading-6 text-gray-700 dark:text-gray-300">{t("install.storeIntro")}</p>
          <a
            href={CHROME_WEB_STORE_URL}
            target="_blank"
            rel="noopener noreferrer"
            className={`self-start ${buttonClassName("primary")}`}
          >
            {t("install.storeCta")}
          </a>
        </div>
        <StepList steps={installSteps} />

        <details className="group rounded-lg border border-gray-200 p-4 dark:border-gray-800">
          <summary className="cursor-pointer text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("installManual.title")}
          </summary>
          <div className="mt-4 flex flex-col gap-4">
            <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("installManual.intro")}</p>
            <StepList steps={installManualSteps} />
          </div>
        </details>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("configure.title")}</h2>
        <StepList steps={configureSteps} />
        <Screenshot src="/help/screenshots/chrome-extension-options.png" alt={t("configure.title")} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("use.title")}</h2>
        <StepList steps={useSteps} />
        <Screenshot src="/help/screenshots/chrome-extension-popup.png" alt={t("use.title")} />
      </section>

      <Callout variant="warning" label={tCommon("warning")} title={t("calloutScraping.title")}>
        {t("calloutScraping.body")}
      </Callout>

      <Callout variant="info" label={tCommon("note")} title={t("calloutDedupe.title")}>
        {t("calloutDedupe.body")}
      </Callout>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("emailForwardingGuide.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("emailForwardingGuide.body")}</p>
        <Screenshot src="/help/screenshots/chrome-extension-email-forwarding.png" alt={t("emailForwardingGuide.title")} />
      </section>
    </div>
  );
}
