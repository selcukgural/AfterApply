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

  // Option 6 on the 2026-09-15 design canvas: the yearly card is the recommended one (it is the
  // cheaper month), each card lists what the plan does — the same three points the gate makes,
  // plus the Monday e-mail — and the two calls to action differ in weight, not in words.
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
      <div className="grid gap-5 pt-3 sm:grid-cols-2">
        {plans.plans.map((plan) => {
          const isYearly = plan.plan === "Yearly";
          const perMonth = isYearly && monthly ? plan.amountMinor / 12 : null;
          const savedMonths = isYearly && monthly && plan.amountMinor < monthly.amountMinor * 12
            ? 12 - Math.round(plan.amountMinor / monthly.amountMinor)
            : 0;
          return (
            <div
              key={plan.plan}
              className={`relative flex flex-col gap-3.5 rounded-xl bg-white p-6 dark:bg-gray-900 ${
                isYearly ? "border-2 border-accent" : "border border-gray-200 dark:border-gray-800"
              }`}
            >
              {isYearly && (
                <span className="absolute -top-3 left-5 rounded-full bg-accent px-2.5 py-0.5 text-xs font-medium text-white">
                  {savedMonths > 0 ? t("recommendedSaves", { months: savedMonths }) : t("recommended")}
                </span>
              )}
              <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t(isYearly ? "yearly" : "monthly")}</h2>
              <p className="text-[28px] leading-8 font-semibold text-gray-900 dark:text-gray-100">
                {formatMinor(plan.amountMinor, plans.currency, locale)}
                <span className="ml-1 text-sm font-normal text-gray-500 dark:text-gray-400">{t(isYearly ? "perYear" : "perMonth")}</span>
              </p>
              <p className="text-xs text-gray-500 dark:text-gray-400">
                {perMonth !== null
                  ? `${t("perMonthEquivalent", { amount: formatMinor(Math.round(perMonth), plans.currency, locale) })} · ${t("kdvIncluded")}`
                  : t("kdvIncluded")}
              </p>
              <ul className="mt-1 flex flex-col gap-2 text-[13px] leading-[18px] text-gray-700 dark:text-gray-300">
                {(["feature1", "feature2", "feature3", "feature4"] as const).map((key) => (
                  <li key={key} className="flex items-start gap-2">
                    <svg viewBox="0 0 24 24" className="mt-px h-4 w-4 shrink-0 text-good" fill="none" stroke="currentColor" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <path d="M5 12.5l4.5 4.5L19 7" />
                    </svg>
                    {t(key)}
                  </li>
                ))}
              </ul>
              <Link
                href={`/pro/checkout?plan=${plan.plan.toLowerCase()}`}
                className={buttonClassName(isYearly ? "primary" : "outline", "mt-auto text-center")}
              >
                {t(active ? "extend" : "choose")}
              </Link>
            </div>
          );
        })}
      </div>
      <p className="flex items-start gap-1.5 text-xs leading-4 text-gray-500 dark:text-gray-400">
        <svg viewBox="0 0 24 24" className="mt-px h-3.5 w-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <rect x="4" y="11" width="16" height="10" rx="2" />
          <path d="M8 11V7a4 4 0 0 1 8 0v4" />
        </svg>
        {t("noAutoRenew")}
      </p>
    </div>
  );
}

export function formatDate(iso: string, locale: string): string {
  return new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "tr-TR", { day: "numeric", month: "long", year: "numeric" }).format(
    new Date(iso),
  );
}
