"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";

interface HelpfulPillProps {
  count: number;
  marked?: boolean;
  busy?: boolean;
  /** A signed-in reader's toggle. */
  onToggle?: () => void;
  /** Where a visitor goes to be able to mark it; used when there is no toggle. */
  signInHref?: string;
  /** The reader wrote it: the count is shown, there is nothing to press (the API refuses it). */
  own?: boolean;
}

/**
 * The "Helpful · N" pill under a review, a salary and a candidate experience — one component so
 * the three read alike (canvas "Son — maaş ve deneyimde Faydalı düğmesi", 2026-09-23). A mark
 * tells the author how many found it helpful that day, never who.
 */
export function HelpfulPill({ count, marked = false, busy = false, onToggle, signInHref, own = false }: HelpfulPillProps) {
  const t = useTranslations("helpful");
  const label = t("label", { count });
  const base = "inline-flex items-center rounded-full border px-3 py-1 text-xs font-medium transition-colors";
  const idle =
    "border-gray-300 text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800";

  if (own) {
    return count > 0 ? (
      <span className={`${base} border-gray-200 text-gray-500 dark:border-gray-800 dark:text-gray-400`} title={t("ownHint")}>
        {label}
      </span>
    ) : null;
  }

  if (onToggle) {
    return (
      <button
        type="button"
        onClick={onToggle}
        disabled={busy}
        aria-pressed={marked}
        className={`${base} disabled:opacity-60 ${marked ? "border-accent bg-accent/10 text-accent-ink" : idle}`}
      >
        {label}
      </button>
    );
  }

  return signInHref ? (
    <Link href={signInHref} className={`${base} ${idle}`}>
      {label}
    </Link>
  ) : null;
}
