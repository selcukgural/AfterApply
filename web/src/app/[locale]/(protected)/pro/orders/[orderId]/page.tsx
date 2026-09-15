"use client";

import { useEffect, useRef, useState } from "react";
import { useParams, useSearchParams } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { OrderStatusBadge } from "@/components/pro/OrderStatusBadge";
import { formatDate } from "@/components/pro/PlanCards";
import { PRO_QUERY_KEYS } from "@/components/pro/useProAccess";
import { buttonClassName } from "@/components/ui/Button";
import { WEEKLY_JOBS_QUERY_KEYS } from "@/components/weeklyJobs/CriteriaForm";
import { ApiError } from "@/lib/api/httpClient";
import { paymentsApi } from "@/lib/api/payments";
import { explainFailure } from "@/lib/payments/failureReason";
import { formatMinor } from "@/lib/payments/money";
import { isWaitingLong, nextPollDelay } from "@/lib/payments/pollSchedule";

const PENDING = new Set(["Pending"]);

/**
 * The result of a checkout. The page never decides anything: it reads the order, which only
 * PayTR's server-to-server notification can move out of Pending, and polls until it does (fast
 * for ninety seconds, then slowly for ten minutes). The `?outcome=` hint PayTR's redirect
 * carries only picks the waiting copy; a "fail" hint with a Paid order shows Paid.
 */
export default function OrderResultPage() {
  const t = useTranslations("payments.result");
  const tPlans = useTranslations("payments.plans");
  const tFailure = useTranslations("payments.failure");
  const locale = useLocale();
  const params = useParams<{ orderId: string }>();
  const searchParams = useSearchParams();
  const queryClient = useQueryClient();
  const outcomeHint = searchParams.get("outcome");
  // When the wait began, taken once on mount (a ref so the poll schedule never re-anchors).
  const startedAt = useRef<number | null>(null);
  useEffect(() => {
    startedAt.current ??= Date.now();
  }, []);
  const [elapsed, setElapsed] = useState(0);

  const order = useQuery({
    queryKey: PRO_QUERY_KEYS.order(params.orderId),
    queryFn: () => paymentsApi.getOrder(params.orderId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (!status || !PENDING.has(status)) {
        return false;
      }
      return nextPollDelay(Date.now() - (startedAt.current ?? Date.now())) ?? false;
    },
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 3,
  });

  // The "still waiting" copy switches when the slow phase begins.
  useEffect(() => {
    if (!order.data || !PENDING.has(order.data.status)) {
      return;
    }
    const timer = window.setInterval(() => setElapsed(Date.now() - (startedAt.current ?? Date.now())), 5000);
    return () => window.clearInterval(timer);
  }, [order.data]);

  // Pro-ness changed: every weekly-jobs screen re-reads its status.
  useEffect(() => {
    if (order.data?.status === "Paid") {
      void queryClient.invalidateQueries({ queryKey: WEEKLY_JOBS_QUERY_KEYS.status });
      void queryClient.invalidateQueries({ queryKey: PRO_QUERY_KEYS.plans });
      void queryClient.invalidateQueries({ queryKey: PRO_QUERY_KEYS.orders });
    }
  }, [order.data?.status, queryClient]);

  if (order.error instanceof ApiError && order.error.status === 404) {
    return (
      <div className="flex flex-col gap-3">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("notFoundTitle")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("notFound")}</p>
        <Link href="/pro" className="text-sm underline">
          {t("backToPlans")}
        </Link>
      </div>
    );
  }

  if (!order.data) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("verifying")}</p>;
  }

  const data = order.data;
  const planName = tPlans(data.plan === "Yearly" ? "yearly" : "monthly");
  const amount = formatMinor(data.amountMinor, data.currency, locale);

  if (data.status === "Paid" || data.status === "RefundRequested" || data.status === "PartiallyRefunded") {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("paidTitle")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">
          {t("paidBody", {
            plan: planName,
            amount,
            date: data.entitlementActiveUntil ? formatDate(data.entitlementActiveUntil, locale) : "—",
          })}
        </p>
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("receiptNote")}</p>
        <div className="flex flex-wrap gap-3">
          <Link href="/weekly-jobs" className={buttonClassName("primary")}>
            {t("goToJobs")}
          </Link>
          <Link href="/pro" className={buttonClassName("secondary")}>
            {t("backToPlans")}
          </Link>
        </div>
      </div>
    );
  }

  if (data.status === "Failed") {
    const explanation = explainFailure(data);
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("failedTitle")}</h1>
        <p role="alert" className="text-sm text-gray-700 dark:text-gray-300">
          {tFailure(explanation.messageKey)}
        </p>
        {explanation.bankMessage && (
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {t("bankSaid")} <q>{explanation.bankMessage}</q>
          </p>
        )}
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("noCharge")}</p>
        <div className="flex flex-wrap gap-3">
          {explanation.retryable && (
            <Link href={`/pro/checkout?plan=${data.plan.toLowerCase()}`} className={buttonClassName("primary")}>
              {t("tryAgain")}
            </Link>
          )}
          <Link href="/pro" className={buttonClassName("secondary")}>
            {t("backToPlans")}
          </Link>
        </div>
      </div>
    );
  }

  if (data.status === "Expired" || data.status === "Cancelled") {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t(data.status === "Expired" ? "expiredTitle" : "cancelledTitle")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("noCharge")}</p>
        <div className="flex flex-wrap gap-3">
          <Link href={`/pro/checkout?plan=${data.plan.toLowerCase()}`} className={buttonClassName("primary")}>
            {t("tryAgain")}
          </Link>
          <Link href="/pro" className={buttonClassName("secondary")}>
            {t("backToPlans")}
          </Link>
        </div>
      </div>
    );
  }

  if (data.status === "Refunded") {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("refundedTitle")}</h1>
        <OrderStatusBadge status={data.status} />
        <Link href="/pro" className="text-sm underline">
          {t("backToPlans")}
        </Link>
      </div>
    );
  }

  // Pending: waiting for PayTR's notification.
  const gaveUp = nextPollDelay(elapsed) === null;
  return (
    <div className="flex flex-col gap-4" aria-live="polite">
      <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">
        {outcomeHint === "fail" ? t("verifyingFailTitle") : t("verifyingTitle")}
      </h1>
      <p className="text-sm text-gray-700 dark:text-gray-300">
        {gaveUp ? t("pendingGaveUp") : isWaitingLong(elapsed) ? t("pendingLong") : t("verifying")}
      </p>
      {!gaveUp && (
        <div className="aa-skeleton h-2 w-48 rounded" aria-hidden="true" />
      )}
      <p className="text-xs text-gray-500 dark:text-gray-400">
        {planName} · {amount}
      </p>
      <div className="flex flex-wrap gap-3">
        <button type="button" onClick={() => order.refetch()} className={buttonClassName("secondary")}>
          {t("refresh")}
        </button>
        <Link href="/pro" className="text-sm underline">
          {t("backToPlans")}
        </Link>
      </div>
    </div>
  );
}
