import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function AfterApplySection() {
  const t = await getTranslations("landing.afterApply");

  const happyPath = [
    t("happyApplied"),
    t("happyScreening"),
    t("happyInterview"),
    t("happyTechnical"),
    t("happyFinal"),
    t("happyOffer"),
    t("happyOutcome"),
  ];

  return (
    <section id="how-it-works" className="scroll-mt-20 py-20">
      <ScrollReveal className="mx-auto flex max-w-5xl flex-col gap-12 px-4">
        <div className="flex flex-col gap-3 text-center">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
        </div>

        <div className="grid gap-8 md:grid-cols-2">
          <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
            <ol className="flex flex-col gap-3">
              {happyPath.map((step, index) => (
                <li key={step} className="flex items-center gap-3 text-sm">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-100 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                    {index + 1}
                  </span>
                  <span className="text-gray-800 dark:text-gray-200">{step}</span>
                </li>
              ))}
            </ol>
          </div>

          <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
            <ol className="flex flex-col gap-3">
              <li className="flex items-center gap-3 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-blue-100 text-xs font-medium text-blue-700 dark:bg-blue-900/40 dark:text-blue-300">
                  1
                </span>
                <span className="text-gray-800 dark:text-gray-200">{t("silentApplied")}</span>
              </li>
              {[0, 1, 2].map((i) => (
                <li key={i} className="flex items-center gap-3 text-sm">
                  <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-amber-100 text-xs font-medium text-amber-700 dark:bg-amber-900/30 dark:text-amber-300">
                    {i + 2}
                  </span>
                  <span className="text-gray-500 dark:text-gray-500">{t("silentWaiting")}</span>
                </li>
              ))}
              <li className="flex items-center gap-3 text-sm">
                <span className="flex h-6 w-6 shrink-0 items-center justify-center rounded-full bg-gray-100 text-xs font-medium text-gray-500 dark:bg-gray-800 dark:text-gray-400">
                  ?
                </span>
                <span className="font-medium text-gray-900 italic dark:text-gray-100">&ldquo;{t("silentQuestion")}&rdquo;</span>
              </li>
            </ol>
          </div>
        </div>

        <p className="mx-auto max-w-2xl text-center text-sm text-gray-500 dark:text-gray-500">{t("note")}</p>
      </ScrollReveal>
    </section>
  );
}
