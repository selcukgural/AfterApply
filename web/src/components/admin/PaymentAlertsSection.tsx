"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Pagination } from "@/components/applications/Pagination";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { ApiError } from "@/lib/api/httpClient";
import { ALERT_PAGE_SIZE, adminPaymentsApi, type AdminAlertFilters } from "@/lib/api/payments";
import type { AdminPaymentOrderResponse, PaymentAlertOutcome, PaymentAlertSortKey } from "@/types/api";

export const ALERT_OUTCOMES: PaymentAlertOutcome[] = ["BadHash", "UnknownOrder", "LateApplied", "Malformed", "Error"];
export const ALERT_SORT_KEYS: PaymentAlertSortKey[] = ["receivedAt", "outcome", "status", "merchantOid"];
export const ALERT_DAY_OPTIONS = [7, 30, 90, 365] as const;

export const DEFAULT_ALERT_FILTERS: AdminAlertFilters = { days: 30, sort: "receivedAt", dir: "desc", page: 1, pageSize: ALERT_PAGE_SIZE };

export const ALERTS_QUERY_KEY = (filters: AdminAlertFilters) => ["admin", "payments", "alerts", filters] as const;

/** Whether anything narrows the list beyond the default window — decides between "no alerts"
 *  and "no alerts match this filter", which are different news for the admin. */
export function isFiltered(filters: AdminAlertFilters): boolean {
  return Boolean(filters.outcome || filters.status || filters.testMode !== undefined || filters.q || filters.days !== DEFAULT_ALERT_FILTERS.days);
}

interface Props {
  fmt: (minor: number, currency: string) => string;
  fmtDate: (iso: string | null) => string;
  onOpenOrder: (orderId: string) => void;
}

/**
 * The payments panel's alert list: the notifications we rejected, could not match or applied
 * late, ten a page so a burst of retries from PayTR (it re-sends an unanswered notification for
 * days) does not bury the one that matters. The filters and the sort go to the server; the amount
 * mismatches below follow the window only — there are never enough of those to page.
 */
export function PaymentAlertsSection({ fmt, fmtDate, onOpenOrder }: Props) {
  const t = useTranslations("adminPayments");
  const [filters, setFilters] = useState<AdminAlertFilters>(DEFAULT_ALERT_FILTERS);
  const [search, setSearch] = useState("");
  const alerts = useQuery({ queryKey: ALERTS_QUERY_KEY(filters), queryFn: () => adminPaymentsApi.getAlerts(filters) });

  const set = (patch: Partial<AdminAlertFilters>) => setFilters((f) => ({ ...f, ...patch, page: 1 }));
  const days = filters.days ?? DEFAULT_ALERT_FILTERS.days!;
  const notifications = alerts.data?.notifications;
  const mismatches = alerts.data?.amountMismatches ?? [];

  return (
    <section className="flex flex-col gap-3" aria-labelledby="payment-alerts-title">
      <h2 id="payment-alerts-title" className="text-base font-semibold text-gray-900 dark:text-gray-100">
        {t("alerts.title", { days })}
      </h2>
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("alerts.hint")}</p>

      <form
        className="flex flex-wrap items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          set({ q: search.trim() || undefined });
        }}
      >
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.days")}
          <Select value={days} onChange={(e) => set({ days: Number(e.target.value) })}>
            {ALERT_DAY_OPTIONS.map((days) => (
              <option key={days} value={days}>
                {t("alerts.daysOption", { days })}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.outcome")}
          <Select
            value={filters.outcome ?? ""}
            onChange={(e) => set({ outcome: (e.target.value || undefined) as PaymentAlertOutcome | undefined })}
            className="min-w-[10rem]"
          >
            <option value="">{t("alerts.allOutcomes")}</option>
            {ALERT_OUTCOMES.map((outcome) => (
              <option key={outcome} value={outcome}>
                {t(`outcome.${outcome}`)}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.status")}
          <Select value={filters.status ?? ""} onChange={(e) => set({ status: e.target.value || undefined })}>
            <option value="">{t("alerts.allStatuses")}</option>
            <option value="success">{t("alerts.statusSuccess")}</option>
            <option value="failed">{t("alerts.statusFailed")}</option>
            <option value="refund">{t("alerts.statusRefund")}</option>
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.mode")}
          <Select
            value={filters.testMode === undefined ? "" : filters.testMode ? "test" : "live"}
            onChange={(e) => set({ testMode: e.target.value === "" ? undefined : e.target.value === "test" })}
          >
            <option value="">{t("alerts.allModes")}</option>
            <option value="live">{t("alerts.modeLive")}</option>
            <option value="test">{t("alerts.modeTest")}</option>
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.search")}
          <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t("alerts.searchPlaceholder")} className="font-mono" />
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          {t("alerts.sort")}
          <Select value={filters.sort} onChange={(e) => set({ sort: e.target.value as PaymentAlertSortKey })}>
            {ALERT_SORT_KEYS.map((key) => (
              <option key={key} value={key}>
                {t(`alerts.sort${key[0].toUpperCase()}${key.slice(1)}`)}
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-xs text-gray-600 dark:text-gray-400">
          <span className="sr-only">{t("alerts.sort")}</span>
          <Select value={filters.dir} onChange={(e) => set({ dir: e.target.value as "asc" | "desc" })}>
            <option value="desc">{t("alerts.dirDesc")}</option>
            <option value="asc">{t("alerts.dirAsc")}</option>
          </Select>
        </label>
        <Button type="submit" variant="secondary">
          {t("alerts.apply")}
        </Button>
        {isFiltered(filters) && (
          <Button
            type="button"
            variant="outline"
            onClick={() => {
              setSearch("");
              setFilters(DEFAULT_ALERT_FILTERS);
            }}
          >
            {t("alerts.reset")}
          </Button>
        )}
      </form>

      {alerts.error && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {alerts.error instanceof ApiError ? alerts.error.message : t("loadError")}
        </p>
      )}

      {notifications && notifications.totalCount === 0 && mismatches.length === 0 && (
        <p className="text-sm text-gray-500 dark:text-gray-400">{isFiltered(filters) ? t("alerts.emptyFiltered") : t("alerts.empty")}</p>
      )}

      {notifications && notifications.totalCount > 0 && (
        <>
          <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-800">
            <table className="w-full min-w-[44rem] border-collapse text-left text-sm">
              <thead>
                <tr className="border-b border-gray-200 text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                  <th className="px-3 py-2 font-medium">{t("alerts.columns.outcome")}</th>
                  <th className="px-3 py-2 font-medium">{t("alerts.columns.merchantOid")}</th>
                  <th className="px-3 py-2 font-medium">{t("alerts.columns.status")}</th>
                  <th className="px-3 py-2 font-medium">{t("alerts.columns.reason")}</th>
                  <th className="px-3 py-2 font-medium">{t("alerts.columns.receivedAt")}</th>
                  <th className="px-3 py-2" />
                </tr>
              </thead>
              <tbody>
                {notifications.items.map((n) => (
                  <tr key={n.id} className="border-b border-gray-100 last:border-0 dark:border-gray-800">
                    <td className="px-3 py-2 font-medium text-gray-900 dark:text-gray-100">
                      {t(`outcome.${n.outcome}`)}
                      {n.testMode && <span className="ml-1 text-xs font-normal text-gray-500 dark:text-gray-400">{t("orders.test")}</span>}
                    </td>
                    <td className="px-3 py-2 font-mono text-xs text-gray-700 dark:text-gray-300">{n.merchantOid}</td>
                    <td className="px-3 py-2 text-gray-700 dark:text-gray-300">{n.status}</td>
                    <td className="px-3 py-2 text-xs text-gray-500 dark:text-gray-400">
                      {n.failedReasonMsg ?? (n.failedReasonCode !== null ? `#${n.failedReasonCode}` : "—")}
                    </td>
                    <td className="px-3 py-2 text-gray-700 dark:text-gray-300">{fmtDate(n.receivedAt)}</td>
                    <td className="px-3 py-2 text-right">
                      {n.orderId && (
                        <button type="button" onClick={() => onOpenOrder(n.orderId!)} className="text-xs underline">
                          {t("orders.open")}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination
            page={notifications.page}
            pageSize={notifications.pageSize}
            totalCount={notifications.totalCount}
            unit="alerts"
            onPageChange={(page) => setFilters((f) => ({ ...f, page }))}
          />
        </>
      )}

      {mismatches.length > 0 && (
        <ul className="divide-y divide-gray-200 rounded-lg border border-amber-300 text-sm dark:divide-gray-800 dark:border-amber-800">
          {mismatches.map((order: AdminPaymentOrderResponse) => (
            <li key={order.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
              <span>
                {t("alerts.mismatch", {
                  expected: fmt(order.amountMinor, order.currency),
                  charged: fmt(order.totalAmountMinor ?? 0, order.currency),
                  email: order.email,
                })}
              </span>
              <button type="button" onClick={() => onOpenOrder(order.id)} className="text-xs underline">
                {t("orders.open")}
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
