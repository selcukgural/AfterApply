import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { guidePath } from "@/lib/guide/articles";

/** How the structured form works, beside it: what is required, what the picks are, why there is
 *  no free text, and that saving publishes. The full guide article linked underneath goes into
 *  how to rate honestly. */
export function ReviewGuidelines() {
  const t = useTranslations("companyReviews.guidelines");
  const locale = useLocale();
  const items = ["ownExperience", "optional", "statements", "noFreeText", "instant"] as const;

  return (
    <aside className="flex flex-col gap-3 rounded-xl border border-accent/30 bg-accent-wash p-4 text-sm dark:border-accent/30">
      <h2 className="font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <p className="text-gray-700 dark:text-gray-300">{t("intro")}</p>
      <ul className="flex list-disc flex-col gap-1.5 pl-5 text-gray-700 dark:text-gray-300">
        {items.map((item) => (
          <li key={item}>{t(item)}</li>
        ))}
      </ul>
      <Link href={guidePath("writing-a-fair-review", locale)} className="font-medium text-accent-ink hover:underline">
        {t("fullGuide")}
      </Link>
    </aside>
  );
}
