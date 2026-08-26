import { getTranslations } from "next-intl/server";
import { ScrollReveal } from "@/components/landing/ScrollReveal";

export async function ProblemSection() {
  const t = await getTranslations("landing.problem");
  const sources = [t("sourceLinkedin"), t("sourceJobSites"), t("sourceCompanyPages"), t("sourceOther")];

  return (
    <section className="border-t border-gray-200 bg-gray-50 py-20 dark:border-gray-800 dark:bg-gray-900/40">
      <ScrollReveal className="mx-auto flex max-w-4xl flex-col items-center gap-10 px-4 text-center">
        <div className="flex flex-col gap-4">
          <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
          <h2 className="text-3xl font-semibold text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h2>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("body1")}</p>
          <p className="mx-auto max-w-2xl text-base text-gray-600 dark:text-gray-400">{t("body2")}</p>
        </div>

        <div className="flex flex-col items-center gap-3">
          <div className="flex flex-wrap justify-center gap-2">
            {sources.map((source) => (
              <span
                key={source}
                className="rounded-full border border-gray-200 bg-white px-3 py-1 text-sm text-gray-600 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-400"
              >
                {source}
              </span>
            ))}
          </div>
          <span aria-hidden="true" className="text-gray-300 dark:text-gray-700">
            ↓
          </span>
          <p className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("outcome")}</p>
          <span aria-hidden="true" className="text-gray-300 dark:text-gray-700">
            ↓
          </span>
          <p className="text-xl font-semibold text-gray-900 italic dark:text-gray-100">&ldquo;{t("question")}&rdquo;</p>
        </div>
      </ScrollReveal>
    </section>
  );
}
