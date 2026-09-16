import { getTranslations } from "next-intl/server";
import { WeeklyJobsHeroCtas } from "@/components/landing/WeeklyJobsHeroCtas";
import { WeeklyJobsSampleList } from "@/components/weeklyJobs/WeeklyJobsSampleList";

/**
 * The first screen once the weekly postings are on sale (2026-09-16, direction B on the design
 * canvas): the paid feature leads, and the page's original promise — "Başvurdun. Peki sonra ne
 * oldu?" with the CV drop zone — follows as the band right under it (HeroSection with `band`).
 *
 * Rendered from the server-side flag (fetchJobSourcesEnabled), never from the client config: a
 * hero that appears after hydration is a hero that jumps, on the one screen where that costs the
 * most. While the flag is off the landing page is exactly what it was.
 *
 * No price here on purpose. A visitor has no account; the price, the terms and the refund rules
 * are on the help topic the second button opens, and the plan is bought from inside the product.
 * The last sentence of the subtitle says the tracker itself stays free — the paid thing leads,
 * but the product must not read as paid.
 */
export async function WeeklyJobsHero() {
  const t = await getTranslations("landing.weeklyHero");
  const samples = [
    { title: t("sample1Title"), company: t("sample1Company"), score: 92 },
    { title: t("sample2Title"), company: t("sample2Company"), score: 81 },
    { title: t("sample3Title"), company: t("sample3Company"), score: 64 },
  ];

  return (
    <section className="relative overflow-hidden">
      <div aria-hidden="true" className="aa-hero-glow pointer-events-none absolute inset-0" />
      <div className="relative mx-auto flex max-w-6xl flex-col items-center gap-12 px-4 pt-16 pb-20 md:flex-row md:items-center md:pt-24 md:pb-28">
        <div className="flex flex-col items-start gap-6 md:w-1/2">
          <span className="inline-flex items-center rounded-full bg-gradient-to-r from-[#1C39B7] to-[#15AAB7] px-2.5 py-0.5 text-xs font-semibold tracking-wide text-white">
            {t("badge")}
          </span>
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h1 className="text-4xl font-semibold tracking-tight text-gray-900 sm:text-5xl dark:text-gray-100">{t("title")}</h1>
          <p className="max-w-xl text-lg text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
          <WeeklyJobsHeroCtas />
        </div>

        <div className="flex justify-center md:w-1/2">
          <div className="aa-card-glow w-full max-w-md rounded-2xl border border-gray-200 bg-white p-5 shadow-lg shadow-gray-900/5 dark:border-gray-800 dark:bg-gray-900">
            <WeeklyJobsSampleList label={t("sampleLabel")} samples={samples} footer={t("sampleFooter")} />
          </div>
        </div>
      </div>
    </section>
  );
}
