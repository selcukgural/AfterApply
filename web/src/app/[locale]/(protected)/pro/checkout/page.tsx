"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { BillingForm, type BillingDetails } from "@/components/pro/BillingForm";
import { PayTrFrame } from "@/components/pro/PayTrFrame";
import { PRO_QUERY_KEYS, useProAccess } from "@/components/pro/useProAccess";
import { Button } from "@/components/ui/Button";
import { useAuth } from "@/lib/auth/AuthContext";
import { ApiError } from "@/lib/api/httpClient";
import { paymentsApi } from "@/lib/api/payments";
import { formatMinor } from "@/lib/payments/money";
import type { CheckoutResponse, ProPlan } from "@/types/api";

const TERMINAL = new Set(["Paid", "Failed", "Expired", "Cancelled", "RefundRequested", "Refunded", "PartiallyRefunded"]);

/**
 * The PayTR checkout in two steps: the billing form, then PayTR's payment frame. While the
 * frame is open the page polls the order every few seconds and moves to the result page the
 * moment PayTR's notification lands — independently of the redirect PayTR performs inside the
 * frame, which is the second way to the same page (/pro/return). Nothing here confirms anything;
 * the result page reads the order and the order is written only by the notification.
 */
export default function CheckoutPage() {
  const t = useTranslations("payments.checkout");
  const tPlans = useTranslations("payments.plans");
  const tCommon = useTranslations("common");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const { plans, isLoading } = useProAccess();

  const plan = parsePlan(searchParams.get("plan"));
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null);
  const [expired, setExpired] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const start = useMutation({
    mutationFn: (details: BillingDetails) => paymentsApi.startCheckout({ plan: plan!, acceptTerms: true, ...details }),
    onSuccess: (response) => {
      setError(null);
      setExpired(false);
      setCheckout(response);
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("providerError")),
  });

  const cancel = useMutation({
    mutationFn: () => paymentsApi.cancelOrder(checkout!.orderId),
    onSettled: async () => {
      await queryClient.invalidateQueries({ queryKey: PRO_QUERY_KEYS.orders });
      router.replace("/pro");
    },
  });

  // Path A to the result: poll while the frame is open.
  const order = useQuery({
    queryKey: PRO_QUERY_KEYS.order(checkout?.orderId ?? ""),
    queryFn: () => paymentsApi.getOrder(checkout!.orderId),
    enabled: checkout !== null && !expired,
    refetchInterval: 3000,
  });

  useEffect(() => {
    if (order.data && TERMINAL.has(order.data.status)) {
      router.replace(`/pro/orders/${order.data.id}`);
    }
  }, [order.data, router]);

  const handleExpired = useCallback(() => setExpired(true), []);

  if (isLoading || !plans) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>;
  }

  const selected = plan ? plans.plans.find((p) => p.plan === plan) : undefined;
  if (!plan || !selected) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("unknownPlan")}</p>
        <Link href="/pro" className="text-sm underline">
          {t("back")}
        </Link>
      </div>
    );
  }

  const initialName = [user?.firstName, user?.lastName].filter(Boolean).join(" ");

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-700 dark:text-gray-300">
          {tPlans(plan === "Yearly" ? "yearly" : "monthly")} · {formatMinor(selected.amountMinor, plans.currency, locale)}{" "}
          <span className="text-gray-500 dark:text-gray-400">{tPlans("kdvIncluded")}</span>
        </p>
      </div>

      {expired ? (
        <div className="flex flex-col gap-3 rounded-lg border border-amber-300 bg-amber-50 p-4 text-sm dark:border-amber-800 dark:bg-amber-950/40">
          <p className="text-amber-800 dark:text-amber-300">{t("expired")}</p>
          <div>
            <Button
              type="button"
              onClick={() => {
                setCheckout(null);
                setExpired(false);
              }}
            >
              {t("restart")}
            </Button>
          </div>
        </div>
      ) : checkout ? (
        <>
          <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("step2")}</p>
          <PayTrFrame
            iframeUrl={checkout.iframeUrl}
            expiresAt={checkout.expiresAt}
            busy={cancel.isPending}
            onCancel={() => cancel.mutate()}
            onExpired={handleExpired}
          />
        </>
      ) : (
        <>
          <p className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("step1")}</p>
          <BillingForm initialName={initialName} busy={start.isPending} error={error} onSubmit={(details) => start.mutate(details)} />
        </>
      )}
    </div>
  );
}

function parsePlan(value: string | null): ProPlan | null {
  switch (value?.toLowerCase()) {
    case "monthly":
      return "Monthly";
    case "yearly":
      return "Yearly";
    default:
      return null;
  }
}
