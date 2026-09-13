"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import type { AdminReviewReport, ReviewReportResolution, ReviewReportStatus } from "@/types/api";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Modal } from "@/components/ui/Modal";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";

const RESOLUTIONS: readonly ReviewReportResolution[] = ["Dismissed", "ChangesRequested", "Removed"];

/** Reports readers filed against published reviews. Resolving one either leaves the review up
 *  (Dismissed) or rejects it with a reason the author reads (the other two). */
export default function AdminReportsPage() {
  const t = useTranslations("adminReviews.reports");
  const tReasons = useTranslations("reviewReportReason");
  const tResolutions = useTranslations("reviewReportResolution");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<ReviewReportStatus>("Open");
  const [page, setPage] = useState(1);
  const [open, setOpen] = useState<AdminReviewReport | null>(null);
  const [resolution, setResolution] = useState<ReviewReportResolution>("Dismissed");
  const [reason, setReason] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const list = useQuery({
    queryKey: ["admin", "reports", { status, page }],
    queryFn: () => adminApi.listReports(status, page),
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 403) && failureCount < 2,
  });

  const resolve = useMutation({
    mutationFn: ({ id, res, why }: { id: string; res: ReviewReportResolution; why: string | null }) => adminApi.resolveReport(id, res, why),
    onSuccess: async () => {
      setOpen(null);
      setReason("");
      setResolution("Dismissed");
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["admin", "reports"] }),
        queryClient.invalidateQueries({ queryKey: ["admin", "reviews"] }),
        queryClient.invalidateQueries({ queryKey: ["admin", "moderationCounts"] }),
      ]);
    },
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));

  if (list.error instanceof ApiError && list.error.status === 403) {
    return (
      <div className="flex flex-col gap-6">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <Card className="flex flex-col gap-1">
          <p className="font-medium text-gray-900 dark:text-gray-100">{t("forbiddenTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("forbiddenBody")}</p>
        </Card>
      </div>
    );
  }

  const needsReason = resolution !== "Dismissed";

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>
      <AdminTabs />

      <div className="flex gap-2 text-sm">
        {(["Open", "Resolved"] as const).map((value) => (
          <button
            key={value}
            type="button"
            aria-pressed={status === value}
            onClick={() => {
              setStatus(value);
              setPage(1);
            }}
            className={`rounded-full border px-3 py-1 ${
              status === value ? "border-accent bg-accent/10 text-accent-ink" : "border-gray-300 text-gray-700 dark:border-gray-700 dark:text-gray-300"
            }`}
          >
            {t(`status.${value}`)}
          </button>
        ))}
      </div>

      {list.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {list.data && list.data.items.length === 0 && <p className="text-sm text-gray-600 dark:text-gray-400">{t("empty")}</p>}

      {list.data && list.data.items.length > 0 && (
        <ul className="flex flex-col gap-3">
          {list.data.items.map((report) => (
            <li key={report.id} className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-white p-4 text-sm dark:border-gray-800 dark:bg-gray-900">
              <div className="flex flex-wrap items-start justify-between gap-2">
                <div className="flex flex-col">
                  <span className="font-semibold text-gray-900 dark:text-gray-100">{tReasons(report.reason)}</span>
                  <span className="text-gray-600 dark:text-gray-400">
                    {report.companyName} · “{report.reviewTitle}”
                  </span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">
                    {report.reporterEmail} · {formatDate(report.reportedAt)}
                  </span>
                </div>
                {report.status === "Open" ? (
                  <Button
                    variant="secondary"
                    onClick={() => {
                      setActionError(null);
                      setOpen(report);
                    }}
                  >
                    {t("resolve")}
                  </Button>
                ) : (
                  <span className="text-xs text-gray-500">{tResolutions(report.resolution ?? "Dismissed")}</span>
                )}
              </div>
              {report.note && <p className="whitespace-pre-wrap text-gray-700 dark:text-gray-300">{report.note}</p>}
              {report.resolutionReason && <p className="text-xs text-gray-500">{report.resolutionReason}</p>}
            </li>
          ))}
        </ul>
      )}

      {list.data && (
        <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} unit="companies" onPageChange={setPage} />
      )}

      {open && (
        <Modal
          title={t("dialog.title")}
          onClose={() => setOpen(null)}
          busy={resolve.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setOpen(null)} disabled={resolve.isPending}>
                {t("dialog.cancel")}
              </Button>
              <Button
                disabled={resolve.isPending || (needsReason && reason.trim().length < 3)}
                onClick={() => resolve.mutate({ id: open.id, res: resolution, why: needsReason ? reason.trim() : null })}
              >
                {t("dialog.confirm")}
              </Button>
            </>
          }
        >
          <div className="flex flex-col gap-4 text-sm">
            <div>
              <h2 className="text-lg font-semibold">{t("dialog.title")}</h2>
              <p className="mt-1 text-gray-600 dark:text-gray-400">
                {tReasons(open.reason)} · {open.companyName} · “{open.reviewTitle}”
              </p>
              {open.note && <p className="mt-2 whitespace-pre-wrap">{open.note}</p>}
            </div>
            <FormField label={t("dialog.resolution")} htmlFor="resolution">
              <Select id="resolution" value={resolution} onChange={(e) => setResolution(e.target.value as ReviewReportResolution)}>
                {RESOLUTIONS.map((value) => (
                  <option key={value} value={value}>
                    {tResolutions(value)}
                  </option>
                ))}
              </Select>
            </FormField>
            <p className="text-xs text-gray-500 dark:text-gray-400">{t(`dialog.explain.${resolution}`)}</p>
            {needsReason && (
              <FormField label={t("dialog.reason")} htmlFor="resolution-reason">
                <Textarea id="resolution-reason" rows={3} value={reason} onChange={(e) => setReason(e.target.value)} placeholder={t("dialog.reasonPlaceholder")} />
              </FormField>
            )}
            {actionError && (
              <p role="alert" className="text-red-600 dark:text-red-400">
                {actionError}
              </p>
            )}
          </div>
        </Modal>
      )}
    </div>
  );
}
