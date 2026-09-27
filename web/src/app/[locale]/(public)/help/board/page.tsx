import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { HelpBreadcrumbJsonLd } from "@/components/seo/HelpBreadcrumbJsonLd";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { StepList } from "@/components/help/StepList";
import { Screenshot } from "@/components/help/Screenshot";
import { Callout } from "@/components/help/Callout";

export async function generateMetadata({ params }: PageProps<"/[locale]/help/board">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/help/board", "helpBoard");
}

/** Step and item keys, listed here because StepList builds its message keys from them. */
const BOARD_HELP_KEYS = {
  columns: ["saved", "applied", "inProgress", "offer", "closed"],
  move: ["step1", "step2", "step3", "step4"],
  arrivals: ["extension", "email", "returned"],
  line: ["silence", "source", "closed"],
} as const;

export default async function BoardHelpPage({ params }: PageProps<"/[locale]/help/board">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("help.board");
  const tCommon = await getTranslations("help.common");

  const steps = (section: keyof typeof BOARD_HELP_KEYS) =>
    BOARD_HELP_KEYS[section].map((key) => ({
      title: t(`${section}.${key}.title`),
      body: t(`${section}.${key}.body`),
    }));

  return (
    <div className="flex flex-col gap-10">
      <HelpBreadcrumbJsonLd path="/help/board" />
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-2xl text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      <Screenshot src="/help/screenshots/board-overview.png" alt={t("screenshots.overview")} />

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("columns.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("columns.body")}</p>
        <StepList steps={steps("columns")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("firstOpen.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("firstOpen.body")}</p>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("move.title")}</h2>
        <StepList steps={steps("move")} />
        <Screenshot src="/help/screenshots/board-card-menu.png" alt={t("screenshots.menu")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("addRemove.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("addRemove.body")}</p>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("arrivals.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("arrivals.body")}</p>
        <StepList steps={steps("arrivals")} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("line.title")}</h2>
        <StepList steps={steps("line")} />
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("filters.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("filters.body")}</p>
        <Screenshot src="/help/screenshots/board-filters.png" alt={t("screenshots.filters")} />
      </section>

      <section className="flex flex-col gap-2">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("phone.title")}</h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("phone.body")}</p>
      </section>

      <Callout variant="info" label={tCommon("note")} title={t("calloutLogos.title")}>
        {t("calloutLogos.body")}
      </Callout>

      <Callout variant="info" label={tCommon("note")} title={t("calloutFilters.title")}>
        {t("calloutFilters.body")}
      </Callout>
    </div>
  );
}
