import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { ScrollReveal } from "@/components/landing/ScrollReveal";
import { LandingIcon, type LandingIcon as LandingIconName } from "@/components/landing/landingIcons";
import { CHROME_WEB_STORE_URL } from "@/lib/constants/chromeWebStore";

export async function FeaturesSection() {
  const t = await getTranslations("landing.features");

  const cards: {
    key: LandingIconName;
    title: string;
    body: string;
    cta?: { label: string; href: string; external?: boolean };
    /** Spans the grid: the newest feature, and the one a visitor can use before signing up. */
    wide?: boolean;
    badge?: string;
  }[] = [
    { key: "tracking", title: t("trackingTitle"), body: t("trackingBody") },
    { key: "timeline", title: t("timelineTitle"), body: t("timelineBody") },
    { key: "analytics", title: t("analyticsTitle"), body: t("analyticsBody") },
    { key: "noResponse", title: t("noResponseTitle"), body: t("noResponseBody") },
    // The only card with an outbound link: the extension is published, and until now the store
    // listing was reachable from the help centre alone — three clicks past the landing page.
    { key: "extension", title: t("extensionTitle"), body: t("extensionBody"), cta: { label: t("extensionCta"), href: CHROME_WEB_STORE_URL, external: true } },
    { key: "cv", title: t("cvTitle"), body: t("cvBody") },
    // Last and full width (2026-09-13): seven cards in two columns would leave one orphan, and
    // this is the card that earns the room — the only feature here that works without an account.
    { key: "companies", title: t("companiesTitle"), body: t("companiesBody"), cta: { label: t("companiesCta"), href: "/companies" }, wide: true, badge: t("companiesBadge") },
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
              className={`flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900 ${card.wide ? "sm:col-span-2" : ""}`}
            >
              <div className="flex items-center gap-3">
                <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-blue-50 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400">
                  <LandingIcon name={card.key} />
                </span>
                {card.badge ? (
                  <span className="ml-auto rounded-full bg-accent-wash px-2 py-0.5 text-xs text-accent-ink">{card.badge}</span>
                ) : null}
              </div>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{card.title}</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">{card.body}</p>
              {card.cta?.external ? (
                <a
                  href={card.cta.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="mt-auto inline-flex w-fit items-center gap-1 pt-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400"
                >
                  {card.cta.label}
                  <span aria-hidden="true">→</span>
                </a>
              ) : card.cta ? (
                <Link
                  href={card.cta.href}
                  className="mt-auto inline-flex w-fit items-center gap-1 pt-1 text-sm font-medium text-blue-600 hover:underline dark:text-blue-400"
                >
                  {card.cta.label}
                  <span aria-hidden="true">→</span>
                </Link>
              ) : null}
            </div>
          ))}
        </div>
      </ScrollReveal>
    </section>
  );
}
