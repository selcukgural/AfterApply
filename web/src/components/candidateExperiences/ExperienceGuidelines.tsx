import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";

/** How a candidate experience works here, beside the form: who may write one, what is required,
 *  what readers see and what they never see. */
export function ExperienceGuidelines() {
  const t = useTranslations("candidateExperiences.guidelines");
  const items = ["optional", "statements", "noNames", "facts", "instant"] as const;

  return (
    <aside className="flex flex-col gap-3 self-start rounded-xl border border-accent/30 bg-accent-wash p-4 text-sm dark:border-accent/30">
      <h2 className="font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="text-gray-700 dark:text-gray-300">{t("intro")}</p>
      <ul className="flex list-disc flex-col gap-1.5 pl-5 text-gray-700 dark:text-gray-300">
        {items.map((item) => (
          <li key={item}>{t(item)}</li>
        ))}
      </ul>
      <Link href="/help/candidate-experiences" className="font-medium text-accent-ink hover:underline">
        {t("fullGuide")}
      </Link>
    </aside>
  );
}
