"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { buttonClassName } from "@/components/ui/Button";
import { formatMinor } from "@/lib/payments/money";
import type { PaymentPlansResponse } from "@/types/api";

/**
 * The two prepaid Pro periods, with the caller's current end date above them when Pro is
 * running. No auto-renewal exists (PayTR iFrame has no subscription), so the copy says "extend"
 * rather than "subscribe" and every card is a link to the checkout for that plan.
 */
export function PlanCards({ plans }: { plans: PaymentPlansResponse }) {
  const t = useTranslations("payments.plans");
  const locale = useLocale();
  const active = plans.entitlement.isActive && plans.entitlement.activeUntil;
  const monthly = plans.plans.find((p) => p.plan === "Monthly");

  return (
    <div className="flex flex-col gap-4">
      {active && (
        <p
          role="status"
          className="rounded-lg border border-good/40 bg-good-wash px-4 py-3 text-sm text-good-ink"
        >
          {t("current", { date: formatDate(plans.entitlement.activeUntil!, locale) })}
        </p>
      )}
      <div className="grid gap-4 sm:grid-cols-2">
        {plans.plans.map((plan) => {
          const isYearly = plan.plan === "Yearly";
          const perMonth = isYearly && monthly ? plan.amountMinor / 12 : null;
          return (
            <div
              key={plan.plan}
              className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900"
            >
              <div className="flex items-baseline justify-between gap-2">
                <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t(isYearly ? "yearly" : "monthly")}</h2>
                {isYearly && monthly && plan.amountMinor < monthly.amountMinor * 12 && (
                  <span className="rounded-full bg-accent-wash px-2 py-0.5 text-xs font-medium text-accent-ink">
                    {t("saves", { months: 12 - Math.round(plan.amountMinor / monthly.amountMinor) })}
                  </span>
                )}
              </div>
              <p className="text-2xl font-semibold text-gray-900 dark:text-gray-100">
                {formatMinor(plan.amountMinor, plans.currency, locale)}
                <span className="ml-1 text-sm font-normal text-gray-500 dark:text-gray-400">{t(isYearly ? "perYear" : "perMonth")}</span>
              </p>
              {perMonth !== null && (
                <p className="text-xs text-gray-500 dark:text-gray-400">
                  {t("perMonthEquivalent", { amount: formatMinor(Math.round(perMonth), plans.currency, locale) })}
                </p>
              )}
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("kdvIncluded")}</p>
              <Link
                href={`/pro/checkout?plan=${plan.plan.toLowerCase()}`}
                className={buttonClassName("primary", "mt-auto text-center")}
              >
                {t(active ? "extend" : "choose")}
              </Link>
            </div>
          );
        })}
      </div>
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("noAutoRenew")}</p>
    </div>
  );
}

export function formatDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "tr-TR", { day: "numeric", month: "long", year: "numeric" }).format(
    new Date(iso),
  );
}
