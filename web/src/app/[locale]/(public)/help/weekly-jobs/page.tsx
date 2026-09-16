import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { StepList } from "@/components/help/StepList";
import { Callout } from "@/components/help/Callout";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/weekly-jobs">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/weekly-jobs", "helpWeeklyJobs");
}

/**
 * The paid weekly postings and the Pro plan, for someone who wants the terms before the button:
 * what the sweep does, what the score is, what it costs, what the CV consent covers, and how a
 * refund works. No screenshot yet — the page ships with the feature's launch and the images
 * follow the first real week (see screenshots/README.md for the recipe).
 */
export default async function WeeklyJobsHelpPage({ params }: PageProps<"/[locale]/help/weekly-jobs">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("help.weeklyJobs");
  const tCommon = await getTranslations("help.common");

  const steps = ["step1", "step2", "step3", "step4"].map((key) => ({ title: t(`how.${key}.title`), body: t(`how.${key}.body`) }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/weekly-jobs" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
        <Link href="/weekly-jobs" className="text-sm font-medium text-accent-ink hover:underline">
          {t("open")}
        </Link>
      </div>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("how.title")}</h2>
        <StepList steps={steps} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("score.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("score.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("plan.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("plan.body")}</p>
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("refund.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("refund.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutConsent.title")}>
        {t("calloutConsent.body")}
      </Callout>
    </div>
  );
}
