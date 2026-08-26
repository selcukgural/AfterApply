import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

const ICONS: Record<string, React.ReactNode> = {
  tracking: (
    <path strokeLinecap="round" strokeLinejoin="round" d="M9 12h6m-6 4h6m-9 4h12a2 2 0 002-2V6a2 2 0 00-2-2H8.5L5 7.5V18a2 2 0 002 2z" />
  ),
  timeline: <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />,
  analytics: <path strokeLinecap="round" strokeLinejoin="round" d="M3 3v18h18M8 17V9m5 8V5m5 12v-6" />,
  noResponse: (
    <path
      strokeLinecap="round"
      strokeLinejoin="round"
      d="M8.228 9c.549-1.165 2.03-2 3.772-2 2.21 0 4 1.343 4 3 0 1.4-1.278 2.575-3.006 2.907-.542.104-.994.54-.994 1.093m0 3h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"
    />
  ),
};

export async function FeaturesSection() {
  const t = await getTranslations("landing.features");

  const cards = [
    { key: "tracking", title: t("trackingTitle"), body: t("trackingBody") },
    { key: "timeline", title: t("timelineTitle"), body: t("timelineBody") },
    { key: "analytics", title: t("analyticsTitle"), body: t("analyticsBody") },
    { key: "noResponse", title: t("noResponseTitle"), body: t("noResponseBody") },
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
                <svg viewBox="0 0 24 24" className="h-5 w-5" fill="none" stroke="currentColor" strokeWidth={1.75} aria-hidden="true">
                  {ICONS[card.key]}
                </svg>
              </span>
              <h3 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{card.title}</h3>
              <p className="text-sm text-gray-600 dark:text-gray-400">{card.body}</p>
            </div>
          ))}
        </div>
      </ScrollReveal>
    </section>
  );
}
