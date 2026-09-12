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
 *
 * The glow behind it (2026-09-12) is the brand gradient, not decoration for its own sake: the page
 * used to open on flat gray-50, which read as a form rather than a product. It is a static CSS
 * gradient layer under the content, so it costs nothing to scroll and has nothing to switch off for
 * reduced motion. No negative z-index — the section creates no stacking context, so a `-z-10` child
 * would fall behind the body background and vanish; the content is simply painted after it.
 */
export async function HeroSection() {
  const t = await getTranslations("landing.hero");

  return (
    <section className="relative overflow-hidden">
      <div aria-hidden="true" className="aa-hero-glow pointer-events-none absolute inset-0" />
      <div className="relative mx-auto flex max-w-6xl flex-col items-center gap-12 px-4 pt-16 pb-20 md:flex-row md:items-center md:pt-24 md:pb-28">
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
      </div>
    </section>
  );
}
