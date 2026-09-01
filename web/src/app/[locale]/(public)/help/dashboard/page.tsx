import { getTranslations } from "next-intl/server";
import { Screenshot } from "@/components/help/Screenshot";

const SECTIONS = ["tiles", "analytics", "responseTime", "statusChart"] as const;

export default async function DashboardHelpPage() {
  const t = await getTranslations("help.dashboard");

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <Screenshot src="/help/screenshots/dashboard-overview.png" alt={t("title")} />

      <div className="flex flex-col gap-6">
        {SECTIONS.map((key) => (
          <section key={key} className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t(`${key}.title`)}</h2>
            <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t(`${key}.body`)}</p>
          </section>
        ))}
      </div>
    </div>
  );
}
