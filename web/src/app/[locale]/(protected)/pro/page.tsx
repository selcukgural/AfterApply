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

  return (
    <div className="flex flex-col gap-8">
      <div className="flex flex-col gap-2">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">{tGate("body")}</p>
        <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-gray-700 dark:text-gray-300">
          <li>{tGate("point1")}</li>
          <li>{tGate("point2")}</li>
          <li>{tGate("point3")}</li>
        </ul>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          {tGate("privacy")}{" "}
          <Link href="/privacy#job-matching" className="underline">
            {tGate("privacyLink")}
          </Link>
        </p>
      </div>

      {isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>}
      {error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {error instanceof ApiError ? error.message : t("loadError")}
        </p>
      )}
      {plans && <PlanCards plans={plans} />}

      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("historyTitle")}</h2>
        <OrderHistory />
      </section>
    </div>
  );
}
