import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function RoadmapSection() {
  const t = await getTranslations("landing.roadmap");

  const todayItems = [
    t("todayTracking"),
    t("todayAnalytics"),
    t("todayImport"),
    t("todayReminders"),
    t("todayMatch"),
    t("todayEmail"),
    t("todayExtension"),
  ];
  const futureItems = [t("futureInsights")];

  return (
    <section className="border-t border-gray-200 bg-gray-50 py-20 dark:border-gray-800 dark:bg-gray-900/40">
      <ScrollReveal className="mx-auto flex max-w-4xl flex-col gap-10 px-4">
        <h2 className="text-center text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>

        <div className="grid gap-6 sm:grid-cols-2">
          <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
            <p className="mb-4 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">
              {t("today")}
            </p>
            <ul className="flex flex-col gap-2.5 text-sm text-gray-800 dark:text-gray-200">
              {todayItems.map((item) => (
                <li key={item} className="flex items-center gap-2">
                  <span className="text-green-600 dark:text-green-400" aria-hidden="true">
                    ✓
                  </span>
                  {item}
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-xl border border-dashed border-gray-300 bg-white/50 p-6 dark:border-gray-700 dark:bg-gray-900/40">
            <p className="mb-4 text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">
              {t("future")}
            </p>
            <ul className="flex flex-col gap-2.5 text-sm text-gray-500 dark:text-gray-400">
              {futureItems.map((item) => (
                <li key={item} className="flex items-center gap-2">
                  <span className="text-gray-400 dark:text-gray-600" aria-hidden="true">
                    ○
                  </span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>
      </ScrollReveal>
    </section>
  );
}
