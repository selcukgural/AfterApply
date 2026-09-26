"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { AdminBlogPostGroup, AdminBlogPostListItem, BlogLanguage, BlogPostKind, BlogPostStatus } from "@/types/api";
import { adminBlogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import { adminPostsPath } from "@/lib/blog/blogPaths";
import { newTranslationHref } from "@/lib/blog/newPostSeed";
import { Card } from "@/components/dashboard/Card";
import { buttonClassName } from "@/components/ui/Button";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { Pagination } from "@/components/applications/Pagination";
import { AdminTabs } from "@/components/admin/AdminTabs";

/**
 * The admin's blog table (2026-09-19, regrouped 2026-09-20): one row per post, its Turkish and
 * English versions side by side — a pair linked as translations is one row, a post without a
 * translation is a row with an empty side. The empty side offers to write the translation,
 * opening the editor already linked; when the other side exists but is another admin's draft
 * (the API withholds it, the link still names it) the side says so instead. Rows are the most
 * recently touched first, whichever side was touched. A draft another admin has not published
 * is not in this list — the API filters, the table cannot leak what the API withholds. "New"
 * opens the editor on nothing — the post is created by the editor's first non-empty save, so
 * a "new post" that is opened and abandoned leaves no row (2026-09-19).
 *
 * The guide has the same table under its own tab (2026-09-26, canvas variant B): one component,
 * one kind per mount — the API keeps the kinds apart, so a guide never shows among blog posts.
 */
export function AdminPostsPage({ kind }: { kind: BlogPostKind }) {
  const t = useTranslations("adminBlog");
  const tGuide = useTranslations("adminGuide");
  // The words that name the section; everything else in the table is the same for both.
  const heading =
    kind === "Guide"
      ? { title: tGuide("title"), subtitle: tGuide("subtitle"), newPost: tGuide("newPost"), empty: tGuide("empty") }
      : { title: t("title"), subtitle: t("subtitle"), newPost: t("newPost"), empty: t("empty") };
  const basePath = adminPostsPath(kind);
  const locale = useLocale();
  const [status, setStatus] = useState<BlogPostStatus | "">("");
  const [page, setPage] = useState(1);

  const list = useQuery({
    queryKey: ["admin", "blog", "grouped", { kind, status, page }],
    queryFn: () => adminBlogApi.listGrouped({ kind, status: status || undefined, page }),
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
  });

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));

  if (list.error instanceof ApiError && list.error.status === 403) {
    return (
      <div className="flex flex-col gap-6">
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{heading.title}</h1>
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
        <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{heading.title}</h1>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{heading.subtitle}</p>
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
        <div className="ml-auto">
          <Link href={`${basePath}/new`} className={buttonClassName("primary")}>
            {heading.newPost}
          </Link>
        </div>
      </div>

      {list.isLoading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
      {list.error && !(list.error instanceof ApiError && list.error.status === 403) && (
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {list.error instanceof ApiError && list.error.status === 404 ? t("disabled") : t("error")}
        </p>
      )}

      {list.data && list.data.items.length === 0 && <p className="text-sm text-gray-600 dark:text-gray-400">{heading.empty}</p>}

      {list.data && list.data.items.length > 0 && (
        <Card className="overflow-x-auto p-0">
          <table className="w-full min-w-[56rem] border-collapse text-sm">
            <thead>
              <tr className="border-b border-gray-200 text-left text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
                <th className="w-[38%] px-4 py-2 font-medium">{t("columns.tr")}</th>
                <th className="w-[38%] px-4 py-2 font-medium">{t("columns.en")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.author")}</th>
                <th className="px-4 py-2 font-medium">{t("columns.updated")}</th>
              </tr>
            </thead>
            <tbody>
              {list.data.items.map((group) => (
                <PostRow key={rowKey(group)} kind={kind} group={group} formatDate={formatDate} />
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

/** A row is keyed by whichever side is there — a pair by its Turkish side. */
function rowKey(group: AdminBlogPostGroup): string {
  return (group.tr ?? group.en)!.id;
}

/** Who wrote the row: one name when both sides share it, both otherwise. */
function authorsOf(group: AdminBlogPostGroup, t: ReturnType<typeof useTranslations<"adminBlog">>): string {
  const names = [group.tr, group.en]
    .filter((side): side is AdminBlogPostListItem => side !== null)
    .map((side) => (side.isMine ? t("mine") : (side.authorEmail ?? "—")));
  return Array.from(new Set(names)).join(" / ");
}

function PostRow({ kind, group, formatDate }: { kind: BlogPostKind; group: AdminBlogPostGroup; formatDate: (iso: string) => string }) {
  const t = useTranslations("adminBlog");
  return (
    <tr className="border-b border-gray-100 last:border-b-0 dark:border-gray-800">
      <LanguageCell kind={kind} language="tr" side={group.tr} other={group.en} formatDate={formatDate} />
      <LanguageCell kind={kind} language="en" side={group.en} other={group.tr} formatDate={formatDate} />
      <td className="max-w-[14rem] truncate px-4 py-3 align-top text-gray-700 dark:text-gray-300">{authorsOf(group, t)}</td>
      <td className="whitespace-nowrap px-4 py-3 align-top text-gray-600 dark:text-gray-400">{formatDate(group.updatedAt)}</td>
    </tr>
  );
}

/**
 * One language's half of a row. With a post: its title (the link into the editor), its status
 * and, once published, the date and the tallies. Without: an offer to write it — unless the
 * other side's link says it exists and is simply not ours to see.
 */
function LanguageCell({
  kind,
  language,
  side,
  other,
  formatDate,
}: {
  kind: BlogPostKind;
  language: BlogLanguage;
  side: AdminBlogPostListItem | null;
  other: AdminBlogPostListItem | null;
  formatDate: (iso: string) => string;
}) {
  const t = useTranslations("adminBlog");
  const router = useRouter();
  const editorPath = `${adminPostsPath(kind)}/${side?.id ?? ""}`;

  if (side === null) {
    if (other === null) {
      return <td className="px-4 py-3 align-top text-gray-500 dark:text-gray-400" />;
    }
    return (
      <td className="px-4 py-3 align-top text-gray-500 dark:text-gray-400">
        {other.translationOfPostId ? (
          <span className="text-xs text-gray-500 dark:text-gray-400">{t("cell.hiddenTranslation")}</span>
        ) : (
          <Link
            href={newTranslationHref(language, other.id, kind)}
            className="inline-flex h-9 items-center gap-2 rounded-md border border-dashed border-gray-300 px-3 text-xs text-gray-600 hover:border-gray-400 hover:text-gray-900 dark:border-gray-700 dark:text-gray-400 dark:hover:border-gray-500 dark:hover:text-gray-100"
          >
            <span aria-hidden="true">+</span>
            {t("cell.addTranslation", { language })}
          </Link>
        )}
      </td>
    );
  }

  return (
    <td
      className="cursor-pointer px-4 py-3 align-top text-gray-900 hover:bg-gray-50 dark:text-gray-100 dark:hover:bg-gray-800/60"
      onClick={() => router.push(editorPath)}
    >
      <div className="flex flex-col gap-1.5">
        <div>
          <Link href={editorPath} className="font-medium underline-offset-2 hover:underline" onClick={(e) => e.stopPropagation()}>
            {side.title || t("untitled")}
          </Link>
          {side.hasUnpublishedChanges && (
            <span className="ml-2 rounded-full bg-warn-wash px-2 py-0.5 text-xs text-warn-ink">{t("unpublishedChanges")}</span>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-gray-500 dark:text-gray-400">
          <span
            className={`rounded-full px-2 py-0.5 font-medium ${
              side.status === "Published" ? "bg-good-wash text-good-ink" : "bg-muted-wash text-muted-ink"
            }`}
          >
            {t(`status.${side.status}`)}
          </span>
          {side.publishedAt ? (
            <>
              <span>{formatDate(side.publishedAt)}</span>
              <span aria-hidden="true" className="text-gray-300 dark:text-gray-600">
                ·
              </span>
              <span className="tabular-nums">{t("cell.likeCount", { count: side.likeCount })}</span>
              <span aria-hidden="true" className="text-gray-300 dark:text-gray-600">
                ·
              </span>
              <span className="tabular-nums">{t("cell.viewCount", { count: side.viewCount })}</span>
            </>
          ) : (
            <span>{t("cell.notPublished")}</span>
          )}
        </div>
      </div>
    </td>
  );
}
