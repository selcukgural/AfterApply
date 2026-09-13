"use client";

import { useCallback, useState } from "react";
import { useSearchParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { AdminCompanyReview } from "@/types/api";
import { adminApi } from "@/lib/api/admin";
import { ApiError } from "@/lib/api/httpClient";
import {
  MODERATION_STATUSES,
  parseModerationFilters,
  toSearchParams,
  withFilterChange,
  type ModerationListFilters,
} from "@/lib/companyReviews/moderationListView";
import { RATING_KEYS } from "@/lib/companyReviews/reviewDraft";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Modal } from "@/components/ui/Modal";
import { Select } from "@/components/ui/Select";
import { Textarea } from "@/components/ui/Textarea";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";
import { ReviewStatusBadge } from "@/components/companyReviews/ReviewStatusBadge";
import { StarRating } from "@/components/companyReviews/StarRating";

/** The moderation queue. Filters live in the URL (see moderationListView); the detail opens in a
 *  modal and is the one screen on the site that shows a review next to its author. */
export default function AdminReviewsPage() {
  const t = useTranslations("adminReviews");
  const tStatus = useTranslations("reviewModerationStatus");
  const tEmployment = useTranslations("employmentStatus");
  const tRatings = useTranslations("companyReviews.ratings");
  const tReasons = useTranslations("reviewReportReason");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();
  const filters = parseModerationFilters(useSearchParams());
  const [openId, setOpenId] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState("");
  const [quotaInput, setQuotaInput] = useState("");
  const [actionError, setActionError] = useState<string | null>(null);

  const apply = useCallback(
    (next: ModerationListFilters) => {
      const params = toSearchParams(next).toString();
      router.push(params ? `/admin/reviews?${params}` : "/admin/reviews");
    },
    [router],
  );

  const list = useQuery({
    queryKey: ["admin", "reviews", filters],
    queryFn: () => adminApi.listReviews(filters),
    retry: (failureCount, err) => !(err instanceof ApiError && err.status === 403) && failureCount < 2,
  });

  const detail = useQuery({
    queryKey: ["admin", "reviews", "detail", openId],
    queryFn: () => adminApi.getReview(openId!),
    enabled: !!openId,
  });

  const refresh = async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["admin", "reviews"] }),
      queryClient.invalidateQueries({ queryKey: ["admin", "moderationCounts"] }),
    ]);
  };

  const approve = useMutation({
    mutationFn: (id: string) => adminApi.approveReview(id),
    onSuccess: async () => {
      setOpenId(null);
      await refresh();
    },
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const reject = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) => adminApi.rejectReview(id, reason),
    onSuccess: async () => {
      setOpenId(null);
      setRejectReason("");
      await refresh();
    },
    onError: (err) => setActionError(err instanceof ApiError ? err.message : t("error")),
  });

  const setQuota = useMutation({
    mutationFn: ({ userId, value }: { userId: string; value: number | null }) => adminApi.setReviewQuota(userId, value),
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

  const review: AdminCompanyReview | undefined = detail.data;

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>
      <AdminTabs />

      <div className="grid gap-3 sm:grid-cols-4">
        <FormField label={t("filters.status")} htmlFor="mod-status">
          <Select id="mod-status" value={filters.status} onChange={(e) => apply(withFilterChange(filters, { status: e.target.value as ModerationListFilters["status"] }))}>
            <option value="">{t("filters.anyStatus")}</option>
            {MODERATION_STATUSES.map((status) => (
              <option key={status} value={status}>
                {tStatus(status)}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label={t("filters.company")} htmlFor="mod-company">
          <Input
            id="mod-company"
            defaultValue={filters.company}
            key={filters.company}
            onBlur={(e) => e.target.value !== filters.company && apply(withFilterChange(filters, { company: e.target.value }))}
            onKeyDown={(e) => e.key === "Enter" && apply(withFilterChange(filters, { company: (e.target as HTMLInputElement).value }))}
          />
        </FormField>
        <FormField label={t("filters.from")} htmlFor="mod-from">
          <Input id="mod-from" type="date" value={filters.from} onChange={(e) => apply(withFilterChange(filters, { from: e.target.value }))} />
        </FormField>
        <FormField label={t("filters.to")} htmlFor="mod-to">
          <Input id="mod-to" type="date" value={filters.to} onChange={(e) => apply(withFilterChange(filters, { to: e.target.value }))} />
        </FormField>
      </div>

      {list.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {list.error && !(list.error instanceof ApiError && list.error.status === 403) && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {list.error instanceof ApiError && list.error.status === 404 ? t("disabled") : t("error")}
        </p>
      )}

      {list.data && list.data.items.length === 0 && <p className="text-sm text-gray-600 dark:text-gray-400">{t("empty")}</p>}

      {list.data && list.data.items.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[44rem] border-collapse text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                <th className="px-4 py-2 font-medium">{t("columns.company")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.title")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.overall")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.status")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.submitted")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.reports")}</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((item) => (
                <tr
                  key={item.id}
                  className="cursor-pointer border-b border-gray-100 last:border-b-0 hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-800/60"
                  onClick={() => {
                    setActionError(null);
                    setOpenId(item.id);
                  }}
                >
                  <td className="px-4 py-2 text-gray-900 dark:text-gray-100">{item.companyName}</td>
                  <td className="max-w-[16rem] truncate px-4 py-2 text-gray-700 dark:text-gray-300">{item.title}</td>
                  <td className="px-4 py-2">{item.overallRating}</td>
                  <td className="px-4 py-2">
                    <ReviewStatusBadge status={item.status} />
                  </td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-400">{formatDate(item.submittedAt)}</td>
                  <td className="px-4 py-2">{item.openReportCount > 0 ? item.openReportCount : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {list.data && (
        <Pagination
          page={list.data.page}
          pageSize={list.data.pageSize}
          totalCount={list.data.totalCount}
          unit="companies"
          onPageChange={(page) => apply({ ...filters, page })}
        />
      )}

      {openId && (
        <Modal
          title={t("detail.title")}
          onClose={() => setOpenId(null)}
          busy={approve.isPending || reject.isPending}
          footer={
            <>
              <Button variant="secondary" onClick={() => setOpenId(null)}>
                {t("detail.close")}
              </Button>
              {review && review.status !== "Rejected" && (
                <Button
                  variant="danger"
                  disabled={rejectReason.trim().length < 3 || reject.isPending}
                  onClick={() => reject.mutate({ id: review.id, reason: rejectReason.trim() })}
                >
                  {t("detail.reject")}
                </Button>
              )}
              {review && review.status !== "Approved" && (
                <Button disabled={approve.isPending} onClick={() => approve.mutate(review.id)}>
                  {t("detail.approve")}
                </Button>
              )}
            </>
          }
        >
          {detail.isLoading && <p className="text-sm text-gray-500">{t("loading")}</p>}
          {review && (
            <div className="flex flex-col gap-4 text-sm">
              <div>
                <h2 className="text-lg font-semibold">{review.title}</h2>
                <p className="text-gray-600 dark:text-gray-400">
                  {review.companySlug ? (
                    <Link href={`/companies/${review.companySlug}`} className="underline-offset-2 hover:underline">
                      {review.companyName}
                    </Link>
                  ) : (
                    review.companyName
                  )}{" "}
                  · {tEmployment(review.employmentStatus)} · {formatDate(review.submittedAt)}
                </p>
                <p className="mt-1 flex items-center gap-2">
                  <ReviewStatusBadge status={review.status} />
                  {review.rejectionReason && <span className="text-xs text-red-700 dark:text-red-400">{review.rejectionReason}</span>}
                </p>
              </div>

              <dl className="grid grid-cols-2 gap-x-4 gap-y-1 text-xs">
                {RATING_KEYS.map((key) => (
                  <div key={key} className="flex items-center justify-between gap-2">
                    <dt className="text-gray-600 dark:text-gray-400">{tRatings(key)}</dt>
                    <dd>
                      <StarRating value={review[key]} label={String(review[key])} />
                    </dd>
                  </div>
                ))}
              </dl>

              <div>
                <h3 className="text-xs font-semibold uppercase text-green-700">{t("detail.pros")}</h3>
                <p className="whitespace-pre-wrap">{review.pros}</p>
              </div>
              <div>
                <h3 className="text-xs font-semibold uppercase text-red-700">{t("detail.cons")}</h3>
                <p className="whitespace-pre-wrap">{review.cons}</p>
              </div>

              {/* The author: admin-only, on purpose. */}
              <div className="rounded-lg border border-gray-200 p-3 dark:border-gray-800">
                <p className="text-xs font-semibold uppercase text-gray-500">{t("detail.author")}</p>
                <p>{review.authorEmail}</p>
                <div className="mt-2 flex items-end gap-2">
                  <FormField label={t("detail.quotaOverride")} htmlFor="quota-override">
                    <Input
                      id="quota-override"
                      type="number"
                      min={0}
                      max={1000}
                      value={quotaInput}
                      onChange={(e) => setQuotaInput(e.target.value)}
                      placeholder={t("detail.quotaPlaceholder")}
                    />
                  </FormField>
                  <Button
                    variant="secondary"
                    disabled={setQuota.isPending}
                    onClick={() =>
                      setQuota.mutate({ userId: review.authorUserId, value: quotaInput === "" ? null : Number(quotaInput) })
                    }
                  >
                    {t("detail.setQuota")}
                  </Button>
                </div>
                {setQuota.data && (
                  <p className="mt-1 text-xs text-gray-600 dark:text-gray-400">
                    {t("detail.quotaResult", { used: setQuota.data.used, limit: setQuota.data.effectiveLimit })}
                  </p>
                )}
              </div>

              {review.reports.length > 0 && (
                <div>
                  <h3 className="text-xs font-semibold uppercase text-gray-500">{t("detail.reports", { count: review.reports.length })}</h3>
                  <ul className="mt-1 flex flex-col gap-1 text-xs">
                    {review.reports.map((report) => (
                      <li key={report.id} className="rounded bg-gray-50 p-2 dark:bg-gray-800">
                        <span className="font-medium">{tReasons(report.reason)}</span> · {report.reporterEmail} · {formatDate(report.reportedAt)}
                        {report.note && <p className="mt-1 whitespace-pre-wrap">{report.note}</p>}
                        {report.status === "Resolved" && <p className="mt-1 text-gray-500">{t("detail.resolvedAs", { resolution: report.resolution ?? "" })}</p>}
                      </li>
                    ))}
                  </ul>
                </div>
              )}

              {review.status !== "Rejected" && (
                <FormField label={t("detail.rejectReason")} htmlFor="reject-reason">
                  <Textarea id="reject-reason" rows={2} value={rejectReason} onChange={(e) => setRejectReason(e.target.value)} placeholder={t("detail.rejectPlaceholder")} />
                </FormField>
              )}

              {actionError && (
                <p role="alert" className="text-red-600 dark:text-red-400">
                  {actionError}
                </p>
              )}
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
