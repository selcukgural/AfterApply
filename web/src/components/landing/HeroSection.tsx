import { getTranslations } from "next-intl/server";
import { CtaButtons } from "@/components/landing/CtaButtons";
import { DashboardPreview } from "@/components/landing/DashboardPreview";

export async function HeroSection() {
  const t = await getTranslations("landing.hero");
  const tNav = await getTranslations("landing.navbar");

  return (
    <section className="mx-auto flex max-w-6xl flex-col items-center gap-12 px-4 pt-16 pb-20 md:flex-row md:items-center md:pt-24 md:pb-28">
      <div className="flex flex-col items-start gap-6 md:w-1/2">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-4xl font-semibold tracking-tight text-gray-900 sm:text-5xl dark:text-gray-100">
          {t("title")}
        </h1>
        <p className="max-w-xl text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        <CtaButtons
          primaryLabel={t("ctaPrimary")}
          secondaryLabel={t("ctaSecondary")}
          secondaryHref="#how-it-works"
          dashboardLabel={tNav("goToDashboard")}
        />
      </div>

      <div className="flex justify-center md:w-1/2">
        <DashboardPreview />
      </div>
    </section>
  );
}
