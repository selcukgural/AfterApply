import { useTranslations } from "next-intl";

/**
 * The "Örnek veri / Sample data" pill every mock on the landing page carries (DECISIONS, Sprint 14).
 * One component so the wording and the placement cannot drift between mocks; positioned by the
 * caller's `relative` box, and deliberately *outside* any `role="img"` wrapper so it is read aloud.
 */
export function SampleDataBadge({ className = "" }: { className?: string }) {
  const t = useTranslations("landing.common");

  return (
    <span
      className={`absolute z-10 rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-500 dark:bg-gray-800 dark:text-gray-400 ${className}`}
    >
      {t("sampleData")}
    </span>
  );
}
