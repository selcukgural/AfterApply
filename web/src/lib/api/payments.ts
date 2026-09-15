import type {
  AdminPaymentOrderDetailResponse,
  AdminPaymentOrderResponse,
  AdminRefundRequest,
  CheckoutResponse,
  MarkRefundedRequest,
  PagedResult,
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

  getAlerts: (days = 30) => apiFetch<PaymentAlertsResponse>(`/api/admin/payments/alerts?days=${days}`),

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
