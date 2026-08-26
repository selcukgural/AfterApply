import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function VisionSection() {
  const t = await getTranslations("landing.vision");

  const steps = [t("stepYours"), t("stepAggregate"), t("stepOutcomes"), t("stepInsights"), t("stepTransparency")];

  return (
    <section className="border-t border-gray-200 bg-gray-50 py-20 dark:border-gray-800 dark:bg-gray-900/40">
      <ScrollReveal className="mx-auto flex max-w-4xl flex-col items-center gap-8 px-4 text-center">
        <div className="flex flex-col gap-4">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("body1")}</p>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("body2")}</p>
        </div>

        <div className="flex flex-wrap items-center justify-center gap-3">
          {steps.map((step, index) => (
            <div key={step} className="flex items-center gap-3">
              <span className="rounded-full border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-300">
                {step}
              </span>
              {index < steps.length - 1 && (
                <span aria-hidden="true" className="text-gray-300 dark:text-gray-700">
                  →
                </span>
              )}
            </div>
          ))}
        </div>

        <p className="mx-auto max-w-xl text-sm text-gray-500 dark:text-gray-500">{t("disclaimer")}</p>
      </ScrollReveal>
    </section>
  );
}
