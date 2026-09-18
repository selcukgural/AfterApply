import { getTranslations } from "next-intl/server";
import { CtaButtons } from "@/components/landing/CtaButtons";
import { ScrollReveal } from "@/components/landing/ScrollReveal";
import { Link } from "@/i18n/navigation";
import { getLocale } from "next-intl/server";
import { aboutPath } from "@/lib/about/path";

export async function FinalCtaSection() {
  const t = await getTranslations("landing.finalCta");
  const tNav = await getTranslations("siteNav");
  const locale = await getLocale();

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
        {/* The one place the landing page points at the story behind it, now that the vision,
            mission and roadmap sections live on the about page. */}
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("who")}{" "}
          <Link href={aboutPath(locale)} className="font-medium text-gray-900 underline underline-offset-2 dark:text-gray-100">
            {t("whoLink")}
          </Link>
        </p>
      </ScrollReveal>
    </section>
  );
}
