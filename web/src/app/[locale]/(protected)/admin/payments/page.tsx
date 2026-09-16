"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { PaymentAlertsSection } from "@/components/admin/PaymentAlertsSection";
import { Pagination } from "@/components/applications/Pagination";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { OrderStatusBadge, statusKey } from "@/components/pro/OrderStatusBadge";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Modal } from "@/components/ui/Modal";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { ApiError } from "@/lib/api/httpClient";
import { adminPaymentsApi, type AdminOrderFilters } from "@/lib/api/payments";
import { formatMinor, formatMonth } from "@/lib/payments/money";
import type { AdminPaymentOrderDetailResponse, AdminPaymentOrderResponse, PaymentOrderStatus, PaymentsMonthSummary } from "@/types/api";

const STATUSES: PaymentOrderStatus[] = ["Pending", "Paid", "Failed", "Expired", "Cancelled", "RefundRequested", "Refunded", "PartiallyRefunded"];
const PAGE_SIZE = 25;

const QUERY_KEYS = {
  summary: ["admin", "payments", "summary"] as const,
  orders: (filters: AdminOrderFilters) => ["admin", "payments", "orders", filters] as const,
  order: (id: string) => ["admin", "payments", "order", id] as const,
  refundRequests: ["admin", "payments", "refundRequests"] as const,
};

type Action =
  | { kind: "refund"; order: AdminPaymentOrderResponse }
  | { kind: "reject"; order: AdminPaymentOrderResponse }
  | { kind: "markRefunded"; order: AdminPaymentOrderResponse }
  | { kind: "cancel"; order: AdminPaymentOrderResponse };

/**
 * The payments panel: this month and the last twelve at a glance, the refund queue, every order
 * with its PayTR timeline, and the things that deserve a look. Every money action starts from an
 * order row here — refunds go through the PayTR refund API from this page and nowhere else, which
 * is what keeps the order row the one true record of what was charged and returned.
 */
export default function AdminPaymentsPage() {
  const t = useTranslations("adminPayments");
  // The filter's labels are the same words the badge uses; they live under payments.status, not here.
  const tStatus = useTranslations("payments.status");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [filters, setFilters] = useState<AdminOrderFilters>({ page: 1, pageSize: PAGE_SIZE });
  const [search, setSearch] = useState("");
  const [openId, setOpenId] = useState<string | null>(null);
  const [action, setAction] = useState<Action | null>(null);

  const summary = useQuery({ queryKey: QUERY_KEYS.summary, queryFn: () => adminPaymentsApi.getSummary(12) });
  const refundRequests = useQuery({ queryKey: QUERY_KEYS.refundRequests, queryFn: adminPaymentsApi.listRefundRequests });
  const orders = useQuery({ queryKey: QUERY_KEYS.orders(filters), queryFn: () => adminPaymentsApi.listOrders(filters) });
  const detail = useQuery({
    queryKey: QUERY_KEYS.order(openId ?? ""),
    queryFn: () => adminPaymentsApi.getOrder(openId!),
    enabled: openId !== null,
  });

  const invalidateAll = async () => {
    await queryClient.invalidateQueries({ queryKey: ["admin", "payments"] });
  };

  const fmt = (minor: number, currency = summary.data?.currency ?? "TL") => formatMinor(minor, currency, locale);
  const fmtDate = (iso: string | null) =>
    iso ? new Intl.DateTimeFormat(locale === "en" ? "en-GB" : "tr-TR", { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso)) : "—";

  const current = summary.data?.currentMonth;

  return (
    <div className="flex flex-col gap-6">
      <AdminTabs />
      <div className="flex flex-col gap-1">
        <h1 className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p role="note" className="rounded-lg border border-amber-300 bg-amber-50 px-3 py-2 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
          {t("panelWarning")}
        </p>
      </div>

      {/* 1. This month + the last twelve */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("summary.thisMonth")}</h2>
        {summary.error && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {summary.error instanceof ApiError ? summary.error.message : t("loadError")}
          </p>
        )}
        {current && (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            <Stat label={t("summary.gross")} value={fmt(current.grossMinor)} hint={t("summary.sales", { count: current.paidCount })} />
            <Stat label={t("summary.refunds")} value={fmt(current.refundedMinor)} hint={t("summary.refundCount", { count: current.refundCount })} />
            <Stat label={t("summary.net")} value={fmt(current.netMinor)} emphasis />
            <Stat label={t("summary.activePro")} value={String(summary.data!.activeProUsers)} />
            <Stat label={t("summary.cancelled")} value={String(current.cancelledCount)} />
            <Stat label={t("summary.failed")} value={String(current.failedCount)} />
            <Stat label={t("summary.openRequests")} value={String(summary.data!.openRefundRequests)} />
            <Stat label={t("summary.testOrders")} value={String(summary.data!.testOrdersLast30Days)} />
          </div>
        )}
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("summary.feeNote")}</p>
        {summary.data && <MonthsTable months={summary.data.months} currency={summary.data.currency} />}
      </section>

      {/* 2. Refund queue */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">
          {t("queue.title")} {refundRequests.data && refundRequests.data.length > 0 && `(${refundRequests.data.length})`}
        </h2>
        {refundRequests.data?.length === 0 && <p className="text-sm text-gray-500 dark:text-gray-400">{t("queue.empty")}</p>}
        {refundRequests.data && refundRequests.data.length > 0 && (
          <ul className="divide-y divide-gray-200 rounded-lg border border-gray-200 dark:divide-gray-800 dark:border-gray-800">
            {refundRequests.data.map((order) => (
              <li key={order.id} className="flex flex-col gap-2 px-3 py-3 text-sm md:flex-row md:items-center md:justify-between">
                <div className="flex flex-col gap-0.5">
                  <span className="font-medium text-gray-900 dark:text-gray-100">
                    {order.email} · {fmt(order.refundableAmountMinor, order.currency)}
                  </span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">
                    {t("queue.requestedAt", { date: fmtDate(order.refundRequestedAt) })} · {t(`plan.${order.plan.toLowerCase()}`)}
                  </span>
                  <span className="text-gray-700 dark:text-gray-300">
                    <q>{order.refundReason}</q>
                  </span>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button variant="secondary" onClick={() => setOpenId(order.id)}>
                    {t("orders.open")}
                  </Button>
                  <Button onClick={() => setAction({ kind: "refund", order })}>{t("refund.full")}</Button>
                  <Button variant="danger" onClick={() => setAction({ kind: "reject", order })}>
                    {t("refund.reject")}
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {/* 3. Orders */}
      <section className="flex flex-col gap-3">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("orders.title")}</h2>
        <form
          className="flex flex-wrap items-end gap-2"
          onSubmit={(e) => {
            e.preventDefault();
            setFilters((f) => ({ ...f, q: search.trim() || undefined, page: 1 }));
          }}
        >
          <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
            {t("orders.status")}
            <Select
              value={filters.status ?? ""}
              onChange={(e) => setFilters((f) => ({ ...f, status: e.target.value || undefined, page: 1 }))}
              className="min-w-[10rem]"
            >
              <option value="">{t("orders.allStatuses")}</option>
              {STATUSES.map((status) => (
                <option key={status} value={status}>
                  {tStatus(statusKey(status))}
                </option>
              ))}
            </Select>
          </label>
          <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
            {t("orders.month")}
            <Input
              type="month"
              value={filters.month ?? ""}
              onChange={(e) => setFilters((f) => ({ ...f, month: e.target.value || undefined, page: 1 }))}
            />
          </label>
          <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
            {t("orders.search")}
            <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("orders.searchPlaceholder")} />
          </label>
          <Button type="submit" variant="secondary">
            {t("orders.apply")}
          </Button>
        </form>
        {orders.data && (
          <>
            <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-800">
              <table className="w-full min-w-[48rem] border-collapse text-left text-sm">
                <thead>
                  <tr className="border-b border-gray-200 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    <th className="px-3 py-2 font-medium">{t("orders.columns.date")}</th>
                    <th className="px-3 py-2 font-medium">{t("orders.columns.email")}</th>
                    <th className="px-3 py-2 font-medium">{t("orders.columns.plan")}</th>
                    <th className="px-3 py-2 font-medium">{t("orders.columns.amount")}</th>
                    <th className="px-3 py-2 font-medium">{t("orders.columns.status")}</th>
                    <th className="px-3 py-2 font-medium">{t("orders.columns.flags")}</th>
                    <th className="px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {orders.data.items.map((order) => (
                    <tr key={order.id} className="border-b border-gray-100 last:border-0 dark:border-gray-800">
                      <td className="px-3 py-2 text-gray-700 dark:text-gray-300">{fmtDate(order.paidAt ?? order.createdAt)}</td>
                      <td className="px-3 py-2 text-gray-900 dark:text-gray-100">{order.email}</td>
                      <td className="px-3 py-2 text-gray-700 dark:text-gray-300">{t(`plan.${order.plan.toLowerCase()}`)}</td>
                      <td className="px-3 py-2 text-gray-900 dark:text-gray-100">
                        {fmt(order.totalAmountMinor ?? order.amountMinor, order.currency)}
                        {order.refundedAmountMinor > 0 && (
                          <span className="ml-1 text-xs text-gray-500 dark:text-gray-400">−{fmt(order.refundedAmountMinor, order.currency)}</span>
                        )}
                      </td>
                      <td className="px-3 py-2">
                        <OrderStatusBadge status={order.status} />
                      </td>
                      <td className="px-3 py-2 text-xs text-gray-500 dark:text-gray-400">
                        {order.testMode && <span className="mr-1">{t("orders.test")}</span>}
                        {order.amountMismatch && <span className="text-amber-700 dark:text-amber-400">{t("orders.mismatch")}</span>}
                      </td>
                      <td className="px-3 py-2 text-right">
                        <button type="button" onClick={() => setOpenId(order.id)} className="text-xs underline">
                          {t("orders.open")}
                        </button>
                      </td>
                    </tr>
                  ))}
                  {orders.data.items.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-3 py-6 text-center text-sm text-gray-500 dark:text-gray-400">
                        {t("orders.empty")}
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
            <Pagination
              page={orders.data.page}
              pageSize={orders.data.pageSize}
              totalCount={orders.data.totalCount}
              unit="orders"
              onPageChange={(page) => setFilters((f) => ({ ...f, page }))}
            />
          </>
        )}
      </section>

      {/* 4. Alerts */}
      <PaymentAlertsSection fmt={fmt} fmtDate={fmtDate} onOpenOrder={setOpenId} />

      {openId && (
        <Modal title={t("detail.title")} onClose={() => setOpenId(null)} footer={<Button variant="secondary" onClick={() => setOpenId(null)}>{t("detail.close")}</Button>}>
          {!detail.data ? (
            <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>
          ) : (
            <OrderDetail
              detail={detail.data}
              fmt={fmt}
              fmtDate={fmtDate}
              onAction={(kind) => setAction({ kind, order: detail.data!.order })}
            />
          )}
        </Modal>
      )}

      {action && (
        <ActionModal
          action={action}
          fmt={fmt}
          onClose={() => setAction(null)}
          onDone={async () => {
            setAction(null);
            await invalidateAll();
          }}
        />
      )}
    </div>
  );
}

function Stat({ label, value, hint, emphasis = false }: { label: string; value: string; hint?: string; emphasis?: boolean }) {
  return (
    <Card className={emphasis ? "border-accent/40" : ""}>
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
      <p className={`mt-1 text-xl font-semibold ${emphasis ? "text-accent-ink" : "text-gray-900 dark:text-gray-100"}`}>{value}</p>
      {hint && <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">{hint}</p>}
    </Card>
  );
}

function MonthsTable({ months, currency }: { months: PaymentsMonthSummary[]; currency: string }) {
  const t = useTranslations("adminPayments.summary");
  const locale = useLocale();
  const fmt = (minor: number) => formatMinor(minor, currency, locale);
  return (
    <Card>
      <CardHeader title={t("last12Months")} />
      <div className="overflow-x-auto">
        <table className="w-full min-w-[40rem] border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-gray-200 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
              <th className="py-2 pr-3 font-medium">{t("month")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("salesColumn")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("gross")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("refunds")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("net")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("cancelled")}</th>
              <th className="py-2 pr-3 font-medium text-right">{t("failed")}</th>
            </tr>
          </thead>
          <tbody>
            {months.map((m) => (
              <tr key={m.month} className="border-b border-gray-100 last:border-0 dark:border-gray-800">
                <td className="py-2 pr-3 text-gray-900 dark:text-gray-100">{formatMonth(m.month, locale)}</td>
                <td className="py-2 pr-3 text-right text-gray-700 dark:text-gray-300">{m.paidCount}</td>
                <td className="py-2 pr-3 text-right text-gray-700 dark:text-gray-300">{fmt(m.grossMinor)}</td>
                <td className="py-2 pr-3 text-right text-gray-700 dark:text-gray-300">{fmt(m.refundedMinor)}</td>
                <td className="py-2 pr-3 text-right font-medium text-gray-900 dark:text-gray-100">{fmt(m.netMinor)}</td>
                <td className="py-2 pr-3 text-right text-gray-700 dark:text-gray-300">{m.cancelledCount}</td>
                <td className="py-2 pr-3 text-right text-gray-700 dark:text-gray-300">{m.failedCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Card>
  );
}

function OrderDetail({
  detail,
  fmt,
  fmtDate,
  onAction,
}: {
  detail: AdminPaymentOrderDetailResponse;
  fmt: (minor: number, currency?: string) => string;
  fmtDate: (iso: string | null) => string;
  onAction: (kind: Action["kind"]) => void;
}) {
  const t = useTranslations("adminPayments");
  const { order, notifications, entitlement } = detail;
  const canRefund = order.refundableAmountMinor > 0;
  const row = (label: string, value: React.ReactNode) => (
    <div className="flex justify-between gap-3 py-1">
      <dt className="text-gray-500 dark:text-gray-400">{label}</dt>
      <dd className="text-right text-gray-900 dark:text-gray-100">{value}</dd>
    </div>
  );
  return (
    <div className="flex flex-col gap-4 text-sm">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-lg font-semibold">{order.email}</h2>
        <OrderStatusBadge status={order.status} />
        {order.testMode && <span className="text-xs text-gray-500">{t("orders.test")}</span>}
      </div>
      <dl className="divide-y divide-gray-100 dark:divide-gray-800">
        {row(t("detail.merchantOid"), <span className="font-mono text-xs">{order.merchantOid}</span>)}
        {row(t("detail.plan"), t(`plan.${order.plan.toLowerCase()}`))}
        {row(t("detail.asked"), fmt(order.amountMinor, order.currency))}
        {row(t("detail.charged"), order.totalAmountMinor === null ? "—" : fmt(order.totalAmountMinor, order.currency))}
        {row(t("detail.refunded"), fmt(order.refundedAmountMinor, order.currency))}
        {row(t("detail.created"), fmtDate(order.createdAt))}
        {row(t("detail.paidAt"), fmtDate(order.paidAt))}
        {order.failedReasonCode !== null && row(t("detail.failure"), `${order.failedReasonCode} ${order.failedReasonMsg ?? ""}`)}
        {row(t("detail.paymentType"), order.paymentType ?? "—")}
        {row(t("detail.terms"), `${order.termsVersion} · ${fmtDate(order.termsAcceptedAt)}`)}
      </dl>
      <div>
        <h3 className="mb-1 font-medium text-gray-900 dark:text-gray-100">{t("detail.billing")}</h3>
        <p className="text-gray-700 dark:text-gray-300">
          {order.billingName}
          <br />
          {order.billingAddress}
          <br />
          {order.billingPhone}
        </p>
      </div>
      <div>
        <h3 className="mb-1 font-medium text-gray-900 dark:text-gray-100">{t("detail.entitlement")}</h3>
        <p className="text-gray-700 dark:text-gray-300">
          {entitlement
            ? t("detail.entitlementState", { active: entitlement.isActive ? "yes" : "no", date: fmtDate(entitlement.activeUntil) })
            : t("detail.noEntitlement")}
          {order.entitlementActiveUntilAfter && (
            <>
              <br />
              {t("detail.entitlementChange", { from: fmtDate(order.entitlementActiveUntilBefore), to: fmtDate(order.entitlementActiveUntilAfter) })}
            </>
          )}
        </p>
      </div>
      {(order.refundReason || order.refundRejectionNote || order.refundReferenceNo) && (
        <div>
          <h3 className="mb-1 font-medium text-gray-900 dark:text-gray-100">{t("detail.refundSection")}</h3>
          <p className="text-gray-700 dark:text-gray-300">
            {order.refundReason && (
              <>
                {t("detail.refundReason")}: <q>{order.refundReason}</q>
                <br />
              </>
            )}
            {order.refundRejectionNote && (
              <>
                {t("detail.refundRejection")}: <q>{order.refundRejectionNote}</q>
                <br />
              </>
            )}
            {order.refundReferenceNo && (
              <>
                {t("detail.refundReference")}: <span className="font-mono text-xs">{order.refundReferenceNo}</span>
              </>
            )}
          </p>
        </div>
      )}
      <div>
        <h3 className="mb-1 font-medium text-gray-900 dark:text-gray-100">{t("detail.timeline")}</h3>
        {notifications.length === 0 ? (
          <p className="text-gray-500 dark:text-gray-400">{t("detail.noNotifications")}</p>
        ) : (
          <ul className="flex flex-col gap-1">
            {notifications.map((n) => (
              <li key={n.id} className="flex flex-wrap justify-between gap-2 text-xs">
                <span>
                  <span className="font-medium text-gray-900 dark:text-gray-100">{t(`outcome.${n.outcome}`)}</span> · {n.status}
                  {n.totalAmountMinor !== null && ` · ${fmt(n.totalAmountMinor, order.currency)}`}
                  {!n.hashValid && <span className="ml-1 text-red-600 dark:text-red-400">{t("detail.badHash")}</span>}
                  {n.failedReasonCode !== null && ` · ${n.failedReasonCode} ${n.failedReasonMsg ?? ""}`}
                </span>
                <span className="text-gray-500 dark:text-gray-400">{fmtDate(n.receivedAt)}</span>
              </li>
            ))}
          </ul>
        )}
      </div>
      <div className="flex flex-wrap gap-2 border-t border-gray-100 pt-3 dark:border-gray-800">
        {canRefund && <Button onClick={() => onAction("refund")}>{t("refund.start")}</Button>}
        {order.status === "RefundRequested" && (
          <Button variant="danger" onClick={() => onAction("reject")}>
            {t("refund.reject")}
          </Button>
        )}
        {canRefund && (
          <Button variant="secondary" onClick={() => onAction("markRefunded")}>
            {t("refund.markRefunded")}
          </Button>
        )}
        {order.status === "Pending" && (
          <Button variant="secondary" onClick={() => onAction("cancel")}>
            {t("detail.cancelOrder")}
          </Button>
        )}
      </div>
    </div>
  );
}

function ActionModal({
  action,
  fmt,
  onClose,
  onDone,
}: {
  action: Action;
  fmt: (minor: number, currency?: string) => string;
  onClose: () => void;
  onDone: () => Promise<void>;
}) {
  const t = useTranslations("adminPayments");
  const { order } = action;
  // The refund policy's amount is the default (everything inside the seven-day window, the
  // unused share of the period after it); the admin can still type any amount up to what is left.
  const policyMinor = action.kind === "refund" ? order.policyRefundMinor : order.refundableAmountMinor;
  const [amount, setAmount] = useState(() => (policyMinor / 100).toFixed(2));
  const [note, setNote] = useState("");
  const [reference, setReference] = useState("");
  const [error, setError] = useState<string | null>(null);

  const amountMinor = Math.round(Number(amount.replace(",", ".")) * 100);
  const amountValid = Number.isFinite(amountMinor) && amountMinor > 0 && amountMinor <= order.refundableAmountMinor;

  const run = useMutation({
    mutationFn: () => {
      switch (action.kind) {
        case "refund":
          return adminPaymentsApi.refund(order.id, { amountMinor: amountMinor === order.refundableAmountMinor ? null : amountMinor });
        case "reject":
          return adminPaymentsApi.rejectRefund(order.id, { note: note.trim() });
        case "markRefunded":
          return adminPaymentsApi.markRefunded(order.id, { amountMinor, referenceNo: reference.trim() });
        case "cancel":
          return adminPaymentsApi.cancelOrder(order.id);
      }
    },
    onSuccess: () => onDone(),
    onError: (err) => setError(err instanceof ApiError ? err.message : t("actionError")),
  });

  const canSubmit =
    action.kind === "cancel" ||
    (action.kind === "reject" && note.trim().length > 0) ||
    (action.kind === "refund" && amountValid) ||
    (action.kind === "markRefunded" && amountValid && reference.trim().length > 0);

  const title = t(`refund.${action.kind}Title`);
  return (
    <Modal
      title={title}
      onClose={onClose}
      busy={run.isPending}
      footer={
        <>
          <Button variant="secondary" onClick={onClose} disabled={run.isPending}>
            {t("refund.cancel")}
          </Button>
          <Button variant={action.kind === "reject" || action.kind === "cancel" ? "danger" : "primary"} onClick={() => run.mutate()} disabled={run.isPending || !canSubmit}>
            {t(`refund.${action.kind}Confirm`)}
          </Button>
        </>
      }
    >
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
        {order.email} · {fmt(order.totalAmountMinor ?? order.amountMinor, order.currency)}
      </p>
      {(action.kind === "refund" || action.kind === "markRefunded") && (
        <>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
            {t(action.kind === "refund" ? "refund.refundBody" : "refund.markRefundedBody", { max: fmt(order.refundableAmountMinor, order.currency) })}
          </p>
          {action.kind === "refund" && (
            <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
              {t("refund.policyAmount", { amount: fmt(order.policyRefundMinor, order.currency) })}
            </p>
          )}
          <label htmlFor="refund-amount" className="mt-3 block text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("refund.amount")}
          </label>
          <Input id="refund-amount" value={amount} onChange={(e) => setAmount(e.target.value)} inputMode="decimal" className="mt-1" />
          {!amountValid && <p className="mt-1 text-xs text-red-600 dark:text-red-400">{t("refund.amountInvalid")}</p>}
        </>
      )}
      {action.kind === "markRefunded" && (
        <>
          <label htmlFor="refund-reference" className="mt-3 block text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("refund.reference")}
          </label>
          <Input id="refund-reference" value={reference} onChange={(e) => setReference(e.target.value)} maxLength={64} className="mt-1" />
        </>
      )}
      {action.kind === "reject" && (
        <>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("refund.rejectBody")}</p>
          <label htmlFor="reject-note" className="mt-3 block text-sm font-medium text-gray-700 dark:text-gray-300">
            {t("refund.note")}
          </label>
          <Textarea id="reject-note" value={note} onChange={(e) => setNote(e.target.value)} maxLength={500} rows={4} className="mt-1" />
        </>
      )}
      {action.kind === "cancel" && <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">{t("detail.cancelBody")}</p>}
      {error && (
        <p role="alert" className="mt-3 text-sm text-red-600 dark:text-red-400">
          {error}
        </p>
      )}
    </Modal>
  );
}
