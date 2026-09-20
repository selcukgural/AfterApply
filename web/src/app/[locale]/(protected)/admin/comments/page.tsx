"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { BlogCommentStatus } from "@/types/api";
import { adminBlogCommentsApi } from "@/lib/api/blogComments";
import { ApiError } from "@/lib/api/httpClient";
import { Card } from "@/components/dashboard/Card";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";

type Filter = "pending" | "reported" | "approved" | "rejected" | "all";

const FILTERS: readonly { key: Filter; status?: BlogCommentStatus; reported?: boolean }[] = [
  { key: "pending", status: "Pending" },
  { key: "reported", reported: true },
  { key: "approved", status: "Approved" },
  { key: "rejected", status: "Rejected" },
  { key: "all" },
];

/**
 * The comment queue (2026-09-20): every reader comment, newest first, opened on "pending" since
 * that is what needs an admin. A row opens the comment; the decision is taken there. The author's
 * address is on this table and nowhere public.
 */
export default function AdminCommentsPage() {
  const t = useTranslations("adminComments");
  const locale = useLocale();
  const router = useRouter();
  const [filter, setFilter] = useState<Filter>("pending");
  const [page, setPage] = useState(1);
  const current = FILTERS.find((f) => f.key === filter)!;

  const list = useQuery({
    queryKey: ["admin", "blog", "comments", { filter, page }],
    queryFn: () => adminBlogCommentsApi.list({ status: current.status, reported: current.reported, page }),
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));
  const statusClass = (status: BlogCommentStatus) =>
    status === "Approved" ? "bg-good-wash text-good-ink" : status === "Rejected" ? "bg-crit-wash text-crit-ink" : "bg-warn-wash text-warn-ink";

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

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>
      <AdminTabs />

      <div className="flex flex-wrap gap-2">
        {FILTERS.map((f) => (
          <button
            key={f.key}
            type="button"
            aria-pressed={filter === f.key}
            onClick={() => {
              setFilter(f.key);
              setPage(1);
            }}
            className={`inline-flex h-8 items-center rounded-full border px-3 text-[13px] font-medium ${
              filter === f.key
                ? "border-accent bg-accent-wash text-accent-ink"
                : "border-gray-300 text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
            }`}
          >
            {t(`filters.${f.key}`)}
          </button>
        ))}
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
          <table className="w-full min-w-[56rem] border-collapse text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                <th className="px-4 py-2 font-medium">{t("columns.author")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.post")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.comment")}</th>
                <th className="px-4 py-2 text-center font-medium">{t("columns.reports")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.status")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.date")}</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((item) => (
                <tr
                  key={item.id}
                  className="cursor-pointer border-b border-gray-100 last:border-b-0 hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-800/60"
                  onClick={() => router.push(`/admin/comments/${item.id}`)}
                >
                  <td className="max-w-[12rem] px-4 py-3 align-top text-gray-900 dark:text-gray-100">
                    <Link href={`/admin/comments/${item.id}`} className="font-medium underline-offset-2 hover:underline" onClick={(e) => e.stopPropagation()}>
                      {item.authorName ?? t("anonymousAuthor")}
                    </Link>
                    <div className="truncate text-xs text-gray-500 dark:text-gray-400">{item.authorEmail}</div>
                  </td>
                  <td className="max-w-[16rem] px-4 py-3 align-top text-gray-700 dark:text-gray-300">{item.postTitle}</td>
                  <td className="max-w-[24rem] px-4 py-3 align-top text-gray-700 dark:text-gray-300">
                    {item.parentCommentId && (
                      <div className="text-xs text-gray-500 dark:text-gray-400">{t("replyTo", { name: item.parentAuthorName ?? t("anonymousAuthor") })}</div>
                    )}
                    <div className="line-clamp-2 whitespace-pre-wrap break-words">{item.content}</div>
                  </td>
                  <td className="px-4 py-3 text-center align-top text-gray-700 dark:text-gray-300">
                    {item.openReportCount > 0 ? (
                      <span className="inline-flex h-5 items-center rounded-full bg-crit-wash px-2 text-xs font-semibold text-crit-ink">{item.openReportCount}</span>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td className="px-4 py-3 align-top text-gray-700 dark:text-gray-300">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusClass(item.status)}`}>{t(`status.${item.status}`)}</span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 align-top text-gray-600 dark:text-gray-400">{formatDate(item.createdAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {list.data && <p className="text-xs text-gray-500 dark:text-gray-400">{t("hint")}</p>}

      {list.data && (
        <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} unit="comments" onPageChange={setPage} />
      )}
    </div>
  );
}
