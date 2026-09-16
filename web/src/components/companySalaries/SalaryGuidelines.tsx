import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";

/** How salary sharing works here, beside the form: who sees it, what they see instead of the
 *  exact figures that could identify someone, and the limits. */
export function SalaryGuidelines() {
  const t = useTranslations("companySalaries.guidelines");
  const items = ["signedIn", "band", "month", "perTitle", "editable"] as const;

  return (
    <aside className="flex flex-col gap-3 self-start rounded-xl border border-accent/30 bg-accent-wash p-4 text-sm dark:border-accent/30">
      <h2 className="font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="text-gray-700 dark:text-gray-300">{t("intro")}</p>
      <ul className="flex list-disc flex-col gap-1.5 pl-5 text-gray-700 dark:text-gray-300">
        {items.map((item) => (
          <li key={item}>{t(item)}</li>
        ))}
      </ul>
      <Link href="/help/company-salaries" className="font-medium text-accent-ink hover:underline">
        {t("fullGuide")}
      </Link>
    </aside>
  );
}
