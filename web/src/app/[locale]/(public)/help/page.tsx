import { getTranslations } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { TopicCard } from "@/components/help/TopicCard";

const TOPIC_LINKS = [
  { href: "/help/getting-started", key: "gettingStarted" },
  { href: "/help/dashboard", key: "dashboard" },
  { href: "/help/tracked-jobs", key: "trackedJobs" },
  { href: "/help/applications", key: "applications" },
  { href: "/help/suggestions", key: "suggestions" },
  { href: "/help/import", key: "import" },
  { href: "/help/settings", key: "settings" },
  { href: "/help/chrome-extension", key: "chromeExtension" },
  { href: "/help/faq", key: "faq" },
] as const;

export default async function HelpOverviewPage() {
  const t = await getTranslations("help.overview");

  const flowSteps = ["step1", "step2", "step3", "step4", "step5"].map((key) => ({
    title: t(`flow.${key}.title`),
    body: t(`flow.${key}.body`),
  }));

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("flow.title")}</h2>
        <StepList steps={flowSteps} />
      </section>

      <section className="flex flex-col gap-4">
        <div className="grid gap-4 sm:grid-cols-2">
          {TOPIC_LINKS.map((topic) => (
            <TopicCard
              key={topic.href}
              href={topic.href}
              title={t(`topics.${topic.key}.title`)}
              description={t(`topics.${topic.key}.description`)}
            />
          ))}
        </div>
      </section>
    </div>
  );
}
