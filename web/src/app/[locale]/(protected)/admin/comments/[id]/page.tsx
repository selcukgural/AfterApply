"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { AdminBlogComment, BlogCommentStatus } from "@/types/api";
import { adminBlogCommentsApi } from "@/lib/api/blogComments";
import { ApiError } from "@/lib/api/httpClient";
import { blogPostPath } from "@/lib/blog/blogPaths";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { AdminTabs } from "@/components/admin/AdminTabs";

/**
 * One comment for the admin (2026-09-20): who wrote it, on which post, the text, its numbers,
 * its reports, and the two decisions — approve or reject — plus dismissing the reports. Every
 * action answers with the fresh comment, which replaces what is on screen.
 */
export default function AdminCommentPage() {
  const { id } = useParams<{ id: string }>();
  const t = useTranslations("adminComments.detail");
  const tStatus = useTranslations("adminComments.status");
  const tReasons = useTranslations("blogComments.reasons");
  const tList = useTranslations("adminComments");
  const locale = useLocale();
  const queryClient = useQueryClient();
  const [actionError, setActionError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["admin", "blog", "comment", id],
    queryFn: () => adminBlogCommentsApi.get(id),
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const apply = (result: AdminBlogComment) => {
    queryClient.setQueryData(["admin", "blog", "comment", id], result);
    void queryClient.invalidateQueries({ queryKey: ["admin", "blog", "comments"], exact: false });
    void queryClient.invalidateQueries({ queryKey: ["admin", "moderationCounts"] });
  };
  const useAction = (run: () => Promise<AdminBlogComment>) =>
    useMutation({
      mutationFn: run,
      onMutate: () => setActionError(null),
      onSuccess: apply,
      onError: (err) => setActionError(err instanceof ApiError && err.message ? err.message : t("actionError")),
    });
  const approve = useAction(() => adminBlogCommentsApi.approve(id));
  const reject = useAction(() => adminBlogCommentsApi.reject(id));
  const dismiss = useAction(() => adminBlogCommentsApi.dismissReports(id));
  const busy = approve.isPending || reject.isPending || dismiss.isPending;

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));
  const statusClass = (status: BlogCommentStatus) =>
    status === "Approved" ? "bg-good-wash text-good-ink" : status === "Rejected" ? "bg-crit-wash text-crit-ink" : "bg-warn-wash text-warn-ink";

  if (query.error instanceof ApiError && query.error.status === 403) {
    return (
      <Card className="flex flex-col gap-1">
        <p className="font-medium text-gray-900 dark:text-gray-100">{tList("forbiddenTitle")}</p>
        <p className="text-sm text-gray-600 dark:text-gray-400">{tList("forbiddenBody")}</p>
      </Card>
    );
  }

  if (query.error) {
    return (
      <div className="flex flex-col gap-4">
        <AdminTabs />
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {query.error instanceof ApiError && query.error.status === 404 ? t("notFound") : tList("error")}
        </p>
        <Link href="/admin/comments" className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {t("back")}
        </Link>
      </div>
    );
  }

  if (!query.data) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tList("loading")}</p>;
  }

  const { comment, parentContent, reports } = query.data;
  const openReports = reports.filter((r) => r.status === "Open").length;
  const field = (label: string, value: React.ReactNode) => (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs text-gray-500 dark:text-gray-400">{label}</span>
      <span className="text-sm text-gray-900 dark:text-gray-100">{value}</span>
    </div>
  );

  return (
    <div className="flex flex-col gap-6">
      <AdminTabs />
      <Link href="/admin/comments" className="text-sm font-medium text-blue-600 dark:text-blue-400">
        {t("back")}
      </Link>

      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="flex flex-col gap-4">
          <div className="flex flex-wrap items-start justify-between gap-3">
            {field(
              t("author"),
              <>
                {comment.authorName ?? tList("anonymousAuthor")}
                {comment.authorEmail && <span className="text-gray-500 dark:text-gray-400"> · {comment.authorEmail}</span>}
              </>,
            )}
            <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${statusClass(comment.status)}`}>{tStatus(comment.status)}</span>
          </div>
          {field(
            t("post"),
            comment.postSlug ? (
              <Link
                href={blogPostPath(comment.postSlug)}
                locale={comment.postLanguage}
                className="text-blue-600 underline-offset-2 hover:underline dark:text-blue-400"
              >
                {comment.postTitle} ↗
              </Link>
            ) : (
              comment.postTitle
            ),
          )}
          <div className="flex flex-col gap-1.5">
            <span className="text-xs text-gray-500 dark:text-gray-400">{t("comment")}</span>
            <p className="whitespace-pre-wrap break-words rounded-lg bg-gray-50 px-3.5 py-3 text-[15px] leading-relaxed text-gray-900 dark:bg-gray-800/60 dark:text-gray-100">
              {comment.content}
            </p>
          </div>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {field(t("written"), formatDate(comment.createdAt))}
            {field(t("edited"), comment.editedAt ? formatDate(comment.editedAt) : "—")}
            {field(t("helpful"), comment.helpfulCount)}
            {field(t("replies"), comment.replyCount)}
          </div>
          <div className="flex flex-wrap gap-2 border-t border-gray-200 pt-4 dark:border-gray-800">
            {comment.status !== "Approved" && (
              <Button onClick={() => approve.mutate()} disabled={busy}>
                {approve.isPending ? t("working") : t("approve")}
              </Button>
            )}
            {comment.status !== "Rejected" && (
              <Button variant="danger" onClick={() => reject.mutate()} disabled={busy}>
                {reject.isPending ? t("working") : t("reject")}
              </Button>
            )}
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("rejectHint")}</p>
          {actionError && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {actionError}
            </p>
          )}
        </Card>

        <div className="flex flex-col gap-6">
          <Card className="flex flex-col gap-3">
            <h2 className="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{t("reports", { count: openReports })}</h2>
            {reports.length === 0 && <p className="text-sm text-gray-500 dark:text-gray-400">{t("noReports")}</p>}
            {reports.map((report) => (
              <div key={report.id} className="flex flex-col gap-1 rounded-lg bg-gray-50 px-3.5 py-3 dark:bg-gray-800/60">
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{tReasons(report.reason)}</span>
                  <span className="text-xs text-gray-500 dark:text-gray-400">{formatDate(report.reportedAt)}</span>
                </div>
                {report.note && <p className="whitespace-pre-wrap break-words text-sm text-gray-700 dark:text-gray-300">“{report.note}”</p>}
                <span className="text-xs text-gray-500 dark:text-gray-400">{t(`reportStatus.${report.status}`)}</span>
              </div>
            ))}
            {openReports > 0 && (
              <div>
                <Button variant="secondary" onClick={() => dismiss.mutate()} disabled={busy}>
                  {dismiss.isPending ? t("working") : t("dismiss")}
                </Button>
              </div>
            )}
            {reports.length > 0 && <p className="text-xs text-gray-500 dark:text-gray-400">{t("dismissHint")}</p>}
          </Card>

          <Card className="flex flex-col gap-2">
            <h2 className="text-[15px] font-semibold text-gray-900 dark:text-gray-100">{t("inReplyTo")}</h2>
            {parentContent ? (
              <p className="whitespace-pre-wrap break-words text-sm text-gray-700 dark:text-gray-300">
                <span className="text-xs text-gray-500 dark:text-gray-400">{comment.parentAuthorName ?? tList("anonymousAuthor")}: </span>
                {parentContent}
              </p>
            ) : (
              <p className="text-sm text-gray-500 dark:text-gray-400">{t("rootComment")}</p>
            )}
          </Card>
        </div>
      </div>
    </div>
  );
}
