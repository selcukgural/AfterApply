"use client";

import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { BillingForm, type BillingDetails } from "@/components/pro/BillingForm";
import { OrderSummary } from "@/components/pro/OrderSummary";
import { PayTrFrame } from "@/components/pro/PayTrFrame";
import { PRO_QUERY_KEYS, useProAccess } from "@/components/pro/useProAccess";
import { Button } from "@/components/ui/Button";
import { useAuth } from "@/lib/auth/AuthContext";
import { ApiError } from "@/lib/api/httpClient";
import { paymentsApi } from "@/lib/api/payments";
import { fieldErrorsOf } from "@/lib/api/validationErrors";
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
  const tCommon = useTranslations("common");
  const router = useRouter();
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const { user } = useAuth();
  const { plans, isLoading } = useProAccess();

  const plan = parsePlan(searchParams.get("plan"));
  const [checkout, setCheckout] = useState<CheckoutResponse | null>(null);
  const [expired, setExpired] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<keyof BillingDetails, string>>>({});

  // What the user typed on their last order, so a second checkout is not typed from scratch.
  const billingDefaults = useQuery({
    queryKey: PRO_QUERY_KEYS.billingDefaults,
    queryFn: paymentsApi.getBillingDefaults,
    staleTime: 60_000,
  });

  const start = useMutation({
    mutationFn: (details: BillingDetails) => paymentsApi.startCheckout({ plan: plan!, acceptTerms: true, ...details }),
    onSuccess: (response) => {
      setError(null);
      setFieldErrors({});
      setExpired(false);
      setCheckout(response);
    },
    onError: (err) => {
      // A validation problem lands under its fields; anything else is one line above the button.
      const perField = fieldErrorsOf(err);
      setFieldErrors(perField);
      setError(Object.keys(perField).length > 0 ? null : err instanceof ApiError ? err.message : t("providerError"));
    },
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

  // The defaults gate the form because BillingForm seeds its state once, on mount. A failed
  // defaults call is not worth blocking a payment over: the form then starts from the profile name.
  if (isLoading || !plans || billingDefaults.isLoading) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>;
  }

  const selected = plan ? plans.plans.find((p) => p.plan === plan) : undefined;
  if (!plan || !selected) {
    return (
      <div className="flex flex-col gap-4">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("unknownPlan")}</p>
        <Link href="/pro" className="text-sm underline">
          {t("back")}
        </Link>
      </div>
    );
  }

  const initial: BillingDetails = {
    billingName: billingDefaults.data?.billingName || [user?.firstName, user?.lastName].filter(Boolean).join(" "),
    billingAddress: billingDefaults.data?.billingAddress ?? "",
    billingPhone: billingDefaults.data?.billingPhone ?? "",
  };

  // 5A on the 2026-09-15 design canvas: the form (or, in step two, PayTR's frame) in a card on
  // the left, the order summary on the right; below `lg` the summary drops under the form. Before
  // this the form stretched naked across the page's full 1024px — a twenty-character phone field
  // a thousand pixels wide.
  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-col gap-1">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          {t("stepOf", { step: checkout && !expired ? 2 : 1, label: checkout && !expired ? t("step2") : t("step1") })}
        </p>
      </div>

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="max-w-2xl">
          {expired ? (
            <div className="flex flex-col gap-3 rounded-xl border border-amber-300 bg-amber-50 p-5 text-sm dark:border-amber-800 dark:bg-amber-950/40">
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
            <PayTrFrame
              iframeUrl={checkout.iframeUrl}
              expiresAt={checkout.expiresAt}
              busy={cancel.isPending}
              onCancel={() => cancel.mutate()}
              onExpired={handleExpired}
            />
          ) : (
            <div className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
              <BillingForm
                initial={initial}
                busy={start.isPending}
                error={error}
                fieldErrors={fieldErrors}
                onSubmit={(details) => start.mutate(details)}
              />
            </div>
          )}
        </div>

        <OrderSummary plans={plans} plan={plan} />
      </div>
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
