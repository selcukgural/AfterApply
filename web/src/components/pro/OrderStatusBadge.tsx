"use client";

import { useTranslations } from "next-intl";
import type { PaymentOrderStatus } from "@/types/api";

const STATUS_COLORS: Record<PaymentOrderStatus, string> = {
  Pending: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Paid: "bg-green-100 text-green-700 dark:bg-green-900/40 dark:text-green-300",
  Failed: "bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300",
  Expired: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
  Cancelled: "bg-gray-100 text-gray-700 dark:bg-gray-800 dark:text-gray-300",
  RefundRequested: "bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  Refunded: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
  PartiallyRefunded: "bg-blue-100 text-blue-700 dark:bg-blue-900/40 dark:text-blue-300",
};

export function OrderStatusBadge({ status }: { status: PaymentOrderStatus }) {
  const t = useTranslations("payments.status");
  return (
    <span className={`inline-block rounded-full px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[status]}`}>
      {t(statusKey(status))}
    </span>
  );
}

/** The message key for a status: "Paid" → "paid", "RefundRequested" → "refundRequested". */
export function statusKey(status: PaymentOrderStatus): string {
  return status.charAt(0).toLowerCase() + status.slice(1);
}
