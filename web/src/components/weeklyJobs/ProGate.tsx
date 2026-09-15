"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { PRO_QUERY_KEYS } from "@/components/pro/useProAccess";
import { useClientConfig } from "@/hooks/useClientConfig";
import { paymentsApi } from "@/lib/api/payments";
import { formatMinor } from "@/lib/payments/money";
import { canSeeProNav } from "@/lib/payments/proNav";

/**
 * What an account without the paid plan sees. With PayTR switched on the right column shows the
 * monthly price and a button to the plan page; without it (a deployment where the checkout does
 * not exist yet) it says "coming soon" — a "Buy" that goes nowhere is worse than that. The
 * description is the feature as it exists, not a promise list.
 */
export function ProGate() {
  const t = useTranslations("weeklyJobs.gate");
  const locale = useLocale();
  const { config, isLoaded } = useClientConfig();
  const canBuy = isLoaded && canSeeProNav(config);
  const plans = useQuery({ queryKey: PRO_QUERY_KEYS.plans, queryFn: paymentsApi.getPlans, enabled: canBuy });
  const monthly = plans.data?.plans.find((p) => p.plan === "Monthly");
  return (
    <div className="grid gap-6 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900 sm:grid-cols-[minmax(0,1fr)_220px]">
      <div className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("body")}</p>
        <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-gray-700 dark:text-gray-300">
          <li>{t("point1")}</li>
          <li>{t("point2")}</li>
          <li>{t("point3")}</li>
        </ul>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {t("privacy")}{" "}
          <Link href="/privacy#job-matching" className="text-accent-ink underline-offset-2 hover:underline">
            {t("privacyLink")}
          </Link>
        </p>
      </div>
      <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-950">
        <span className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("plan")}</span>
        {canBuy ? (
          <>
            {monthly && plans.data && (
              <p className="text-lg font-semibold text-gray-900 dark:text-gray-100">
                {formatMinor(monthly.amountMinor, plans.data.currency, locale)}
                <span className="ml-1 text-xs font-normal text-gray-500 dark:text-gray-400">{t("perMonth")}</span>
              </p>
            )}
            <Link href="/pro" className={buttonClassName("primary", "text-center")}>
              {t("upgrade")}
            </Link>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("upgradeNote")}</p>
          </>
        ) : (
          <>
            <span className={buttonClassName("primary", "text-center opacity-40")} aria-disabled="true">
              {t("comingSoon")}
            </span>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t("comingSoonNote")}</p>
          </>
        )}
      </div>
    </div>
  );
}
