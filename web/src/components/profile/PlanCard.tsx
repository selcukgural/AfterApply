"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { authApi } from "@/lib/api/auth";
import { PRO_NAV_HREF } from "@/lib/payments/proNav";
import { resolvePlanCardState } from "@/lib/profile/planCardState";
import { useClientConfig } from "@/hooks/useClientConfig";
import { buttonClassName } from "@/components/ui/Button";

export const PLAN_QUERY_KEY = ["users", "me", "plan"] as const;

/**
 * One line about the plan: Pro until when, or free. Read from /api/users/me/plan, which answers
 * regardless of the payments and job-sources flags — so a person whose Pro was granted by hand
 * sees it even while the checkout is off. The buy button only appears while the checkout exists
 * (canSeeProNav); when it doesn't, the line states the plan and promises nothing.
 */
export function PlanCard() {
  const t = useTranslations("profile.plan");
  const locale = useLocale();
  const { config } = useClientConfig();
  const { data: plan } = useQuery({ queryKey: PLAN_QUERY_KEY, queryFn: authApi.plan });
  const { state, canBuy } = resolvePlanCardState(plan, config);

  const until = plan?.activeUntil ? new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(new Date(plan.activeUntil)) : "";

  const badge =
    state === "active" ? (
      <span className="inline-flex items-center gap-1 rounded-full bg-accent-wash px-2.5 py-0.5 text-xs font-medium text-accent-ink">
        <svg viewBox="0 0 24 24" className="h-3 w-3" fill="currentColor" aria-hidden="true">
          <path d="M12 2l2.9 6.6 7.1.7-5.3 4.8 1.6 7L12 17.6 5.7 21l1.6-7L2 9.3l7.1-.7z" />
        </svg>
        {t("proBadge")}
      </span>
    ) : (
      <span className="inline-flex rounded-full bg-muted-wash px-2.5 py-0.5 text-xs font-medium text-muted-ink">{t("freeBadge")}</span>
    );

  return (
    <section
      aria-label={t("title")}
      className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-gray-200 bg-white px-6 py-4 dark:border-gray-800 dark:bg-gray-900"
    >
      <div className="flex flex-wrap items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
        {badge}
        {state === "active" && <span>{t("activeUntil", { date: until })}</span>}
        {state === "expired" && <span>{t("endedOn", { date: until })}</span>}
        {state === "free-can-buy" && <span>{t("freeBody")}</span>}
      </div>
      {state === "active" && (
        <Link href={PRO_NAV_HREF} className="text-sm text-accent-ink underline-offset-2 hover:underline">
          {t("manage")}
        </Link>
      )}
      {state !== "active" && canBuy && (
        <Link href={PRO_NAV_HREF} className={buttonClassName("primary")}>
          {state === "expired" ? t("buyAgain") : t("goPro")}
        </Link>
      )}
    </section>
  );
}
