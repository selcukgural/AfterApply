import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";
import { CHROME_WEB_STORE_URL } from "@/lib/constants/chromeWebStore";

export async function FeaturesSection() {
  const t = await getTranslations("landing.features");

  const cards: { key: LandingIconName; title: string; body: string; cta?: string }[] = [
    { key: "tracking", title: t("trackingTitle"), body: t("trackingBody") },
    { key: "timeline", title: t("timelineTitle"), body: t("timelineBody") },
    { key: "analytics", title: t("analyticsTitle"), body: t("analyticsBody") },
    { key: "noResponse", title: t("noResponseTitle"), body: t("noResponseBody") },
    // The only card with an outbound link: the extension is published, and until now the store
    // listing was reachable from the help centre alone — three clicks past the landing page.
    { key: "extension", title: t("extensionTitle"), body: t("extensionBody"), cta: t("extensionCta") },
    { key: "cv", title: t("cvTitle"), body: t("cvBody") },
  ];

  return (
    <section id="features" className="scroll-mt-20 border-t border-gray-200 py-20 dark:border-gray-800">
      <ScrollReveal className="mx-auto flex max-w-6xl flex-col gap-12 px-4">
        <div className="flex flex-col gap-3 text-center">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
        </div>

        <div className="grid gap-6 sm:grid-cols-2">
          {cards.map((card) => (
            <div
              key={card.key}
              className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900"
            >
              <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400">
                <LandingIcon name={card.key} />
              </span>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{card.title}</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">{card.body}</p>
              {card.cta ? (
                <a
                  href={CHROME_WEB_STORE_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-auto inline-flex w-fit items-center gap-1 pt-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400"
                >
                  {card.cta}
                  <span aria-hidden="true">→</span>
                </a>
              ) : null}
            </div>
          ))}
        </div>
      </ScrollReveal>
    </section>
  );
}
