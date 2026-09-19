"use client";

import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { BlogLanguage, BlogPostStatus } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import { Card } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";

/**
 * The admin's blog table (2026-09-19): the caller's drafts and every published post, most
 * recently touched first. A draft another admin has not published is not in this list — the API
 * filters, the table cannot leak what the API withholds. A row opens the editor; "new" creates an
 * empty draft in the chosen language and opens it.
 */
export default function AdminBlogPage() {
  const t = useTranslations("adminBlog");
  const locale = useLocale();
  const router = useRouter();
  const [status, setStatus] = useState<BlogPostStatus | "">("");
  const [lang, setLang] = useState<BlogLanguage | "">("");
  const [page, setPage] = useState(1);
  const [createError, setCreateError] = useState<string | null>(null);

  const list = useQuery({
    queryKey: ["admin", "blog", { status, lang, page }],
    queryFn: () => adminBlogApi.list({ status: status || undefined, lang: lang || undefined, page }),
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const create = useMutation({
    mutationFn: (language: BlogLanguage) => adminBlogApi.create(language),
    onSuccess: (post) => router.push(`/admin/blog/${post.id}`),
    onError: (err) => setCreateError(err instanceof ApiError ? err.message : t("error")),
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

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
      </div>
      <AdminTabs />

      <div className="flex flex-wrap items-end gap-3">
        <FormField label={t("filters.status")} htmlFor="blog-status">
          <Select
            id="blog-status"
            value={status}
            onChange={(e) => {
              setStatus(e.target.value as BlogPostStatus | "");
              setPage(1);
            }}
          >
            <option value="">{t("filters.statusAll")}</option>
            <option value="Draft">{t("status.Draft")}</option>
            <option value="Published">{t("status.Published")}</option>
          </Select>
        </FormField>
        <FormField label={t("filters.language")} htmlFor="blog-lang">
          <Select
            id="blog-lang"
            value={lang}
            onChange={(e) => {
              setLang(e.target.value as BlogLanguage | "");
              setPage(1);
            }}
          >
            <option value="">{t("filters.languageAll")}</option>
            <option value="tr">{t("language.tr")}</option>
            <option value="en">{t("language.en")}</option>
          </Select>
        </FormField>
        <div className="ml-auto flex flex-wrap gap-2">
          <Button variant="primary" disabled={create.isPending} onClick={() => create.mutate("tr")}>
            {create.isPending ? t("creating") : `${t("newPost")} · ${t("newPostTr")}`}
          </Button>
          <Button variant="outline" disabled={create.isPending} onClick={() => create.mutate("en")}>
            {t("newPostEn")}
          </Button>
        </div>
      </div>
      {createError && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {createError}
        </p>
      )}

      {list.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {list.error && !(list.error instanceof ApiError && list.error.status === 403) && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {list.error instanceof ApiError && list.error.status === 404 ? t("disabled") : t("error")}
        </p>
      )}

      {list.data && list.data.items.length === 0 && <p className="text-sm text-gray-600 dark:text-gray-400">{t("empty")}</p>}

      {list.data && list.data.items.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[48rem] border-collapse text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                <th className="px-4 py-2 font-medium">{t("columns.title")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.status")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.language")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.author")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.updated")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.published")}</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((item) => (
                <tr
                  key={item.id}
                  className="cursor-pointer border-b border-gray-100 last:border-b-0 hover:bg-gray-50 dark:border-gray-800 dark:hover:bg-gray-800/60"
                  onClick={() => router.push(`/admin/blog/${item.id}`)}
                >
                  <td className="max-w-[24rem] px-4 py-2 text-gray-900 dark:text-gray-100">
                    <Link href={`/admin/blog/${item.id}`} className="font-medium underline-offset-2 hover:underline" onClick={(e) => e.stopPropagation()}>
                      {item.title || t("untitled")}
                    </Link>
                    {item.hasUnpublishedChanges && (
                      <span className="ml-2 rounded-full bg-warn-wash px-2 py-0.5 text-xs text-warn-ink">{t("unpublishedChanges")}</span>
                    )}
                  </td>
                  <td className="px-4 py-2 text-gray-700 dark:text-gray-300">
                    <span
                      className={`rounded-full px-2 py-0.5 text-xs font-medium ${
                        item.status === "Published" ? "bg-good-wash text-good-ink" : "bg-muted-wash text-muted-ink"
                      }`}
                    >
                      {t(`status.${item.status}`)}
                    </span>
                  </td>
                  <td className="px-4 py-2 text-gray-700 dark:text-gray-300">{t(`language.${item.language}`)}</td>
                  <td className="max-w-[14rem] truncate px-4 py-2 text-gray-700 dark:text-gray-300">
                    {item.isMine ? t("mine") : (item.authorEmail ?? "—")}
                  </td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-400">{formatDate(item.updatedAt)}</td>
                  <td className="px-4 py-2 text-gray-600 dark:text-gray-400">{item.publishedAt ? formatDate(item.publishedAt) : "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      {list.data && (
        <Pagination page={list.data.page} pageSize={list.data.pageSize} totalCount={list.data.totalCount} unit="posts" onPageChange={setPage} />
      )}
    </div>
  );
}
