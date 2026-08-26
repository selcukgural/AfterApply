import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function LinkedInImportSection() {
  const t = await getTranslations("landing.linkedinImport");
  const tCommon = await getTranslations("landing.common");

  const steps = [t("stepUpload"), t("stepAnalyzing"), t("stepDedup"), t("stepComplete")];
  const results = [
    { label: t("totalRecords"), value: "1.136" },
    { label: t("newApplications"), value: "1.020" },
    { label: t("duplicates"), value: "116" },
    { label: t("invalid"), value: "0" },
  ];

  return (
    <section className="border-t border-gray-200 bg-gray-50 py-20 dark:border-gray-800 dark:bg-gray-900/40">
      <ScrollReveal className="mx-auto flex max-w-5xl flex-col items-center gap-10 px-4 text-center">
        <div className="flex flex-col gap-4">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
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

        <div className="w-full max-w-lg rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
          <div className="mb-4 flex items-center justify-between">
            <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("resultLabel")}</p>
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400">
              {tCommon("sampleData")}
            </span>
          </div>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            {results.map((result) => (
              <div key={result.label}>
                <p className="text-xl font-semibold text-gray-900 dark:text-gray-100">{result.value}</p>
                <p className="text-xs text-gray-500 dark:text-gray-400">{result.label}</p>
              </div>
            ))}
          </div>
        </div>
      </ScrollReveal>
    </section>
  );
}
