import { useTranslations } from "next-intl";

/**
 * The small "Pro" mark next to a paid item in the navigation. Says "this one has a gate", not
 * "this is locked": the item stays visible and clickable for everyone, and the gate page does
 * the explaining. The pill is the accent-wash one the plan cards and the landing page use.
 */
export function ProBadge() {
  const t = useTranslations("nav");
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-accent-wash px-1.5 py-px text-[11px] font-medium leading-[14px] text-accent-ink">
      <svg viewBox="0 0 24 24" className="h-3 w-3" fill="currentColor" aria-hidden="true">
        <path d="M12 2l2.9 6.6 7.1.7-5.3 4.8 1.6 7L12 17.6 5.7 21l1.6-7L2 9.3l7.1-.7z" />
      </svg>
      {t("proBadge")}
    </span>
  );
}
