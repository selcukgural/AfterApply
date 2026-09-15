"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Button } from "@/components/ui/Button";
import { Modal } from "@/components/ui/Modal";
import { Textarea } from "@/components/ui/Textarea";
import { ApiError } from "@/lib/api/httpClient";
import { paymentsApi } from "@/lib/api/payments";
import { formatMinor } from "@/lib/payments/money";
import type { PaymentOrderResponse } from "@/types/api";
import { OrderStatusBadge } from "./OrderStatusBadge";
import { PRO_QUERY_KEYS } from "./useProAccess";

/**
 * The user's payments, newest first, with "ask for a refund" on the ones that allow it. The
 * request is a request: an admin decides and the money moves through PayTR, so the row only
 * changes to "refund requested" here and the rest arrives by e-mail.
 */
export function OrderHistory({ compact = false }: { compact?: boolean }) {
  const t = useTranslations("payments.history");
  const tPlans = useTranslations("payments.plans");
  const locale = useLocale();
  const orders = useQuery({ queryKey: PRO_QUERY_KEYS.orders, queryFn: paymentsApi.listOrders });
  const [refunding, setRefunding] = useState<PaymentOrderResponse | null>(null);

  if (orders.isLoading) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
  }

  const items = orders.data ?? [];
  if (items.length === 0) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

  const shown = compact ? items.slice(0, 5) : items;
  return (
    <div className="flex flex-col gap-2">
      <ul className="divide-y divide-gray-200 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
        {shown.map((order) => (
          <li key={order.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2 text-sm">
            <div className="flex flex-col">
              <span className="font-medium text-gray-900 dark:text-gray-100">
                {tPlans(order.plan === "Yearly" ? "yearly" : "monthly")} · {formatMinor(order.amountMinor, order.currency, locale)}
              </span>
              <span className="text-xs text-gray-500 dark:text-gray-400">
                {new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "tr-TR", { dateStyle: "medium", timeStyle: "short" }).format(
                  new Date(order.paidAt ?? order.createdAt),
                )}
                {order.refundedAmountMinor > 0 && ` · ${t("refunded", { amount: formatMinor(order.refundedAmountMinor, order.currency, locale) })}`}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <OrderStatusBadge status={order.status} />
              {order.status === "Pending" && (
                <Link href={`/pro/orders/${order.id}`} className="text-xs underline">
                  {t("open")}
                </Link>
              )}
              {order.canRequestRefund && (
                <button type="button" onClick={() => setRefunding(order)} className="text-xs text-gray-600 underline dark:text-gray-400">
                  {t("requestRefund")}
                </button>
              )}
            </div>
          </li>
        ))}
      </ul>
      {compact && items.length > shown.length && (
        <Link href="/pro" className="text-xs underline">
          {t("seeAll")}
        </Link>
      )}
      {refunding && <RefundRequestModal order={refunding} onClose={() => setRefunding(null)} />}
    </div>
  );
}

function RefundRequestModal({ order, onClose }: { order: PaymentOrderResponse; onClose: () => void }) {
  const t = useTranslations("payments.refund");
  const queryClient = useQueryClient();
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const request = useMutation({
    mutationFn: () => paymentsApi.requestRefund(order.id, { reason: reason.trim() }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: PRO_QUERY_KEYS.orders });
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  return (
    <Modal
      title={t("title")}
      onClose={onClose}
      busy={request.isPending}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={request.isPending}>
            {t("cancel")}
          </Button>
          <Button onClick={() => request.mutate()} disabled={request.isPending || reason.trim().length === 0}>
            {t("submit")}
          </Button>
        </>
      }
    >
      <h2 className="text-lg font-semibold">{t("title")}</h2>
      <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("body")}</p>
      <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">
        <Link href="/refund-policy" target="_blank" className="underline">
          {t("policyLink")}
        </Link>
      </p>
      <label htmlFor="refund-reason" className="mt-3 block text-sm font-medium text-gray-700 dark:text-gray-300">
        {t("reason")}
      </label>
      <Textarea id="refund-reason" value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} rows={4} className="mt-1" />
      {error && (
        <p role="alert" className="mt-2 text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </Modal>
  );
}
