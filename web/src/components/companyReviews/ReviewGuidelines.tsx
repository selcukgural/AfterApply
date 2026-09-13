import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { guidePath } from "@/lib/guide/articles";

/** The "write something fair and useful" guide beside the form. Every line is also a rule the
 *  moderator applies, so the reader knows why a review might come back; the full guide article
 *  linked underneath is where the rules come with before/after examples. */
export function ReviewGuidelines() {
  const t = useTranslations("companyReviews.guidelines");
  const locale = useLocale();
  const items = ["ownExperience", "specific", "noNames", "noInsults", "balanced", "noAds"] as const;

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
      <p className="text-xs text-gray-600 dark:text-gray-400">{t("moderation")}</p>
    </aside>
  );
}
