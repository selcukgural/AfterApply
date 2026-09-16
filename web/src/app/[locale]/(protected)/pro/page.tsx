"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { OrderHistory } from "@/components/pro/OrderHistory";
import { PlanCards } from "@/components/pro/PlanCards";
import { useProAccess } from "@/components/pro/useProAccess";
import { ApiError } from "@/lib/api/httpClient";

/**
 * The Pro plan: what it is (the same three points the weekly-jobs gate shows), the two prepaid
 * periods with prices, and the user's payment history underneath.
 */
export default function ProPage() {
  const t = useTranslations("payments.plans");
  const tGate = useTranslations("weeklyJobs.gate");
  const tCommon = useTranslations("common");
  const { plans, isLoading, error } = useProAccess();

  // The three points the gate makes now live inside each plan card (PlanCards), so the page
  // itself is one sentence, the cards, the privacy line and the history — at the width of the
  // other single-purpose pages rather than the full board.
  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-8">
      <div className="flex flex-col gap-1.5">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="max-w-[62ch] text-sm text-gray-600 dark:text-gray-400">{t("intro")}</p>
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error instanceof ApiError ? error.message : t("loadError")}
        </p>
      )}
      {plans && <PlanCards plans={plans} />}

      <p className="text-xs text-gray-500 dark:text-gray-400">
        {tGate("privacy")}{" "}
        <Link href="/privacy#job-matching" className="underline">
          {tGate("privacyLink")}
        </Link>
      </p>

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("historyTitle")}</h2>
        <OrderHistory />
      </section>
    </div>
  );
}
