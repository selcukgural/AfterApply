import { getTranslations } from "next-intl/server";
import { CtaButtons } from "@/components/landing/CtaButtons";
import { ScrollReveal } from "@/components/landing/ScrollReveal";
import { Link } from "@/i18n/navigation";
import { getLocale } from "next-intl/server";
import { aboutPath } from "@/lib/about/path";

/**
 * The closing band: the three privacy promises, then the call to action (2026-09-22).
 *
 * The promises used to be a section of their own, with a full-size heading and three cards, right
 * before this one — two closing bands in a row, both ending in a link. They belong together: the
 * privacy line is the reason somebody accepts the invitation under it, so it reads as the terms of
 * the offer rather than as a separate claim. Same three points, same words, same policy link; what
 * went is one heading, one <section> and one screen of scrolling.
 */
export async function FinalCtaSection() {
  const t = await getTranslations("landing.finalCta");
  const tPrivacy = await getTranslations("landing.privacy");
  const tNav = await getTranslations("siteNav");
  const locale = await getLocale();

  const promises = [
    { title: tPrivacy("privateTitle"), body: tPrivacy("privateBody") },
    { title: tPrivacy("anonymousTitle"), body: tPrivacy("anonymousBody") },
    { title: tPrivacy("deleteTitle"), body: tPrivacy("deleteBody") },
  ];

  return (
    <section className="border-t border-gray-200 bg-blue-50 py-20 dark:border-gray-800 dark:bg-blue-950/20">
      <ScrollReveal className="mx-auto flex max-w-4xl flex-col gap-12 px-4">
        <div className="flex flex-col gap-6">
          <div className="flex flex-col gap-1 text-center">
            <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{tPrivacy("eyebrow")}</span>
            <p className="text-lg font-semibold text-gray-900 dark:text-gray-100">{tPrivacy("title")}</p>
          </div>

          <div className="grid gap-6 sm:grid-cols-3">
            {promises.map((promise) => (
              <div key={promise.title} className="flex flex-col gap-1 text-center sm:text-left">
                <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{promise.title}</h3>
                <p className="text-sm text-gray-600 dark:text-gray-400">{promise.body}</p>
              </div>
            ))}
          </div>

          <p className="text-center">
            <Link href="/privacy" className="text-sm text-blue-600 hover:underline dark:text-blue-400">
              {tPrivacy("link")}
            </Link>
          </p>
        </div>

        <div className="flex flex-col items-center gap-6 border-t border-blue-200/70 pt-12 text-center dark:border-blue-900/40">
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
        </div>
      </ScrollReveal>
    </section>
  );
}
