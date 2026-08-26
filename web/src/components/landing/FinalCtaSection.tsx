import { getTranslations } from "next-intl/server";
import { CtaButtons } from "@/components/landing/CtaButtons";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function FinalCtaSection() {
  const t = await getTranslations("landing.finalCta");
  const tNav = await getTranslations("landing.navbar");

  return (
    <section className="border-t border-gray-200 bg-blue-50 py-20 dark:border-gray-800 dark:bg-blue-950/20">
      <ScrollReveal className="mx-auto flex max-w-2xl flex-col items-center gap-6 px-4 text-center">
        <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
        <p className="text-base text-gray-600 dark:text-gray-400">{t("body")}</p>
        <CtaButtons
          primaryLabel={t("button")}
          secondaryLabel={t("secondary")}
          secondaryHref="#how-it-works"
          dashboardLabel={tNav("goToDashboard")}
        />
      </ScrollReveal>
    </section>
  );
}
