import { getTranslations } from "next-intl/server";

const QUESTION_KEYS = ["q1", "q2", "q3", "q4", "q5", "q6", "q7", "q8"] as const;

export default async function FaqHelpPage() {
  const t = await getTranslations("help.faq");

  return (
    <div className="flex flex-col gap-10">
      <div className="flex flex-col gap-3">
        <span className="text-sm font-medium text-blue-600 dark:text-blue-400">{t("eyebrow")}</span>
        <h1 className="text-3xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      </div>

      <div className="flex flex-col divide-y divide-gray-200 dark:divide-gray-800">
        {QUESTION_KEYS.map((key) => (
          <div key={key} className="flex flex-col gap-2 py-5 first:pt-0">
            <h2 className="font-medium text-gray-900 dark:text-gray-100">{t(`${key}.question`)}</h2>
            <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t(`${key}.answer`)}</p>
          </div>
        ))}
      </div>
    </div>
  );
}
