"use client";

import { useLocale, useTranslations } from "next-intl";
import { formatMinor } from "@/lib/payments/money";
import type { PaymentPlansResponse, ProPlan } from "@/types/api";

/**
 * The checkout's right-hand column: what is being bought and for how much, restated where the
 * eye goes while filling in the form. The numbers come from the plans response — the same
 * source the plan cards use — so the summary cannot disagree with the button that led here.
 */
export function OrderSummary({ plans, plan }: { plans: PaymentPlansResponse; plan: ProPlan }) {
  const t = useTranslations("payments.summary");
  const tPlans = useTranslations("payments.plans");
  const locale = useLocale();
  const selected = plans.plans.find((p) => p.plan === plan);
  if (!selected) {
    return null;
  }

  const amount = formatMinor(selected.amountMinor, plans.currency, locale);

  return (
    <aside className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-gray-50 p-5 dark:border-gray-800 dark:bg-gray-900/60">
      <p className="text-xs font-semibold tracking-wide text-gray-500 uppercase dark:text-gray-400">{t("title")}</p>
      <div className="flex items-baseline justify-between text-sm text-gray-900 dark:text-gray-100">
        <span>{t("line", { plan: tPlans(plan === "Yearly" ? "yearly" : "monthly") })}</span>
        <span>{amount}</span>
      </div>
      <div className="flex items-baseline justify-between text-xs text-gray-500 dark:text-gray-400">
        <span>{t("vat")}</span>
        <span>{t("vatIncluded")}</span>
      </div>
      <div className="border-t border-gray-200 dark:border-gray-800" />
      <div className="flex items-baseline justify-between text-base font-semibold text-gray-900 dark:text-gray-100">
        <span>{t("total")}</span>
        <span>{amount}</span>
      </div>
      <p className="text-xs leading-4 text-gray-500 dark:text-gray-400">
        {t(plan === "Yearly" ? "periodYearly" : "periodMonthly")}
      </p>
      <p className="flex items-start gap-1.5 text-xs leading-4 text-gray-500 dark:text-gray-400">
        <svg viewBox="0 0 24 24" className="mt-px h-3.5 w-3.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <rect x="4" y="11" width="16" height="10" rx="2" />
          <path d="M8 11V7a4 4 0 0 1 8 0v4" />
        </svg>
        {t("secure")}
      </p>
    </aside>
  );
}
