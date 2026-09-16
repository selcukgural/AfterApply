import type {
  AdminPaymentOrderDetailResponse,
  AdminPaymentOrderResponse,
  AdminRefundRequest,
  BillingDefaultsResponse,
  CheckoutResponse,
  MarkRefundedRequest,
  PagedResult,
  PaymentAlertOutcome,
  PaymentAlertSortKey,
  PaymentAlertsResponse,
  PaymentOrderResponse,
  PaymentPlansResponse,
  PaymentsSummaryResponse,
  RejectRefundRequest,
  RequestRefundRequest,
  StartCheckoutRequest,
} from "@/types/api";
import { apiFetch } from "./httpClient";

/** The Pro plan's PayTR checkout. Every route answers 404 while the server flag is off; the pages
 *  check `config.payments.enabled` before calling any of these. */
export const paymentsApi = {
  getPlans: () => apiFetch<PaymentPlansResponse>("/api/payments/plans"),

  getBillingDefaults: () => apiFetch<BillingDefaultsResponse>("/api/payments/billing-defaults"),

  startCheckout: (request: StartCheckoutRequest) =>
    apiFetch<CheckoutResponse>("/api/payments/checkout", { method: "POST", body: JSON.stringify(request) }),

  getOrder: (orderId: string) => apiFetch<PaymentOrderResponse>(`/api/payments/orders/${orderId}`),

  listOrders: () => apiFetch<PaymentOrderResponse[]>("/api/payments/orders"),

  cancelOrder: (orderId: string) => apiFetch<void>(`/api/payments/orders/${orderId}/cancel`, { method: "POST" }),

  requestRefund: (orderId: string, request: RequestRefundRequest) =>
    apiFetch<PaymentOrderResponse>(`/api/payments/orders/${orderId}/refund-request`, {
      method: "POST",
      body: JSON.stringify(request),
    }),
};

export interface AdminOrderFilters {
  status?: string;
  month?: string;
  q?: string;
  page?: number;
  pageSize?: number;
}

/** The alert list's filters, as the query string names them. Everything is optional: the server
 *  defaults to the last 30 days, every alert outcome, ten rows, newest first. */
export interface AdminAlertFilters {
  days?: number;
  outcome?: PaymentAlertOutcome;
  /** PayTR's status word ("success" / "failed"), or "refund" for a refund call PayTR refused. */
  status?: string;
  /** true = test-mode rows only, false = live only, undefined = both. */
  testMode?: boolean;
  /** Matched inside the merchant_oid. */
  q?: string;
  sort?: PaymentAlertSortKey;
  dir?: "asc" | "desc";
  page?: number;
  pageSize?: number;
}

export const ALERT_PAGE_SIZE = 10;

export function alertQueryString(filters: AdminAlertFilters): string {
  const params = new URLSearchParams();
  if (filters.days) params.set("days", String(filters.days));
  if (filters.outcome) params.set("outcome", filters.outcome);
  if (filters.status) params.set("status", filters.status);
  if (filters.testMode !== undefined) params.set("testMode", String(filters.testMode));
  if (filters.q) params.set("q", filters.q);
  if (filters.sort) params.set("sort", filters.sort);
  if (filters.dir) params.set("dir", filters.dir);
  params.set("page", String(filters.page ?? 1));
  params.set("pageSize", String(filters.pageSize ?? ALERT_PAGE_SIZE));
  return params.toString();
}

export const adminPaymentsApi = {
  getSummary: (months = 12) => apiFetch<PaymentsSummaryResponse>(`/api/admin/payments/summary?months=${months}`),

  listOrders: (filters: AdminOrderFilters) => {
    const params = new URLSearchParams();
    if (filters.status) params.set("status", filters.status);
    if (filters.month) params.set("month", filters.month);
    if (filters.q) params.set("q", filters.q);
    params.set("page", String(filters.page ?? 1));
    params.set("pageSize", String(filters.pageSize ?? 25));
    return apiFetch<PagedResult<AdminPaymentOrderResponse>>(`/api/admin/payments/orders?${params.toString()}`);
  },

  getOrder: (orderId: string) => apiFetch<AdminPaymentOrderDetailResponse>(`/api/admin/payments/orders/${orderId}`),

  listRefundRequests: () => apiFetch<AdminPaymentOrderResponse[]>("/api/admin/payments/refund-requests"),

  getAlerts: (filters: AdminAlertFilters = {}) =>
    apiFetch<PaymentAlertsResponse>(`/api/admin/payments/alerts?${alertQueryString(filters)}`),

  refund: (orderId: string, request: AdminRefundRequest) =>
    apiFetch<AdminPaymentOrderResponse>(`/api/admin/payments/orders/${orderId}/refund`, {
      method: "POST",
      body: JSON.stringify(request),
    }),

  rejectRefund: (orderId: string, request: RejectRefundRequest) =>
    apiFetch<AdminPaymentOrderResponse>(`/api/admin/payments/orders/${orderId}/reject-refund`, {
      method: "POST",
      body: JSON.stringify(request),
    }),

  markRefunded: (orderId: string, request: MarkRefundedRequest) =>
    apiFetch<AdminPaymentOrderResponse>(`/api/admin/payments/orders/${orderId}/mark-refunded`, {
      method: "POST",
      body: JSON.stringify(request),
    }),

  cancelOrder: (orderId: string) =>
    apiFetch<AdminPaymentOrderResponse>(`/api/admin/payments/orders/${orderId}/cancel`, { method: "POST" }),
};
