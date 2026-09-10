import { getTranslations } from "next-intl/server";
import { HeroCtaButtons } from "@/components/landing/HeroCtaButtons";
import { HeroCvDropzone } from "@/components/landing/HeroCvDropzone";

/**
 * The first screen, and since 2026-09-10 it hands over something rather than asking for something.
 *
 * The right-hand side used to be a picture of the dashboard — a product nobody can open without an
 * account, shown to people who do not have one. It is now a place to put a CV, and the primary
 * button is the scan rather than registration. For a product with no users, an account is worth
 * nothing until someone has seen what it is for; this is the cheapest thing the site owns that is
 * worth something on its own.
 */
export async function HeroSection() {
  const t = await getTranslations("landing.hero");

  return (
    <section className="mx-auto flex max-w-6xl flex-col items-center gap-12 px-4 pt-16 pb-20 md:flex-row md:items-center md:pt-24 md:pb-28">
      <div className="flex flex-col items-start gap-6 md:w-1/2">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-4xl font-semibold tracking-tight text-gray-900 sm:text-5xl dark:text-gray-100">
          {t("title")}
        </h1>
        <p className="max-w-xl text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        <HeroCtaButtons />
      </div>

      <div className="flex justify-center md:w-1/2">
        <HeroCvDropzone />
      </div>
    </section>
  );
}
