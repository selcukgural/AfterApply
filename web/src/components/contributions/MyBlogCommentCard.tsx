"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { MyBlogComment } from "@/types/api";
import { blogCommentsApi } from "@/lib/api/blogComments";
import { blogPostPath } from "@/lib/blog/blogPaths";
import { ContributionKindBadge } from "@/components/contributions/ContributionKindBadge";
import { CommentForm } from "@/components/blog/comments/CommentForm";

interface MyBlogCommentCardProps {
  comment: MyBlogComment;
  onEdited: (comment: MyBlogComment) => void;
}

/**
 * A blog comment on the author's contributions list (2026-09-20): the post it is on, the text,
 * and what its status allows — an edit while it waits, a link to it once it is on the site, the
 * moderation message once it is not. Nothing deletes a comment (decided 2026-09-20).
 */
export function MyBlogCommentCard({ comment, onEdited }: MyBlogCommentCardProps) {
  const t = useTranslations("companyReviews.mine.blogComment");
  const tComments = useTranslations("blogComments");
  const locale = useLocale();
  const [editing, setEditing] = useState(false);

  const formatDate = (iso: string) => new Intl.DateTimeFormat(locale, { dateStyle: "medium", timeStyle: "short" }).format(new Date(iso));
  const statusClass = {
    Pending: "bg-warn-wash text-warn-ink",
    Approved: "bg-good-wash text-good-ink",
    Rejected: "bg-crit-wash text-crit-ink",
  }[comment.status];

  return (
    <li className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex flex-col gap-1">
          <span className="flex flex-wrap items-center gap-2">
            <ContributionKindBadge kind="BlogComment" />
            <Link
              href={blogPostPath(comment.postSlug)}
              locale={comment.postLanguage}
              className="font-semibold text-gray-900 underline-offset-2 hover:underline dark:text-gray-100"
            >
              {comment.postTitle}
            </Link>
          </span>
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {formatDate(comment.createdAt)}
            {comment.editedAt && <> · {t("edited", { date: formatDate(comment.editedAt) })}</>}
            {comment.parentCommentId && (
              <> · {comment.parentAuthorName ? t("replyTo", { name: comment.parentAuthorName }) : t("replyToReader")}</>
            )}
          </span>
        </div>
        <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${statusClass}`}>{tComments(`status.${comment.status}`)}</span>
      </div>

      {editing ? (
        <CommentForm
          label={tComments("editLabel")}
          placeholder={tComments("placeholder")}
          submitLabel={tComments("save")}
          initialValue={comment.content}
          autoFocus
          onCancel={() => setEditing(false)}
          onSubmit={async (content) => {
            const edited = await blogCommentsApi.edit(comment.id, { content });
            onEdited({ ...comment, content: edited.content, editedAt: edited.editedAt });
            setEditing(false);
          }}
        />
      ) : (
        <p className="whitespace-pre-wrap break-words text-sm leading-6 text-gray-700 dark:text-gray-300">{comment.content}</p>
      )}

      {comment.status === "Rejected" && (
        <p className="rounded-lg bg-crit-wash px-3 py-2 text-sm text-crit-ink">{t("rejectedMessage")}</p>
      )}

      {!editing && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {comment.status === "Pending" && t("pendingHint")}
            {comment.status === "Approved" && (
              <>
                {t("stats", { helpful: comment.helpfulCount, replies: comment.replyCount })} · {t("approvedHint")}
              </>
            )}
          </p>
          <div className="flex gap-2">
            {comment.status === "Pending" && (
              <button
                type="button"
                className="inline-flex h-8 items-center rounded-md border border-gray-300 px-3 text-xs font-medium text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
                onClick={() => setEditing(true)}
              >
                {t("edit")}
              </button>
            )}
            {comment.status === "Approved" && (
              <Link
                href={blogPostPath(comment.postSlug)}
                locale={comment.postLanguage}
                className="inline-flex h-8 items-center rounded-md border border-gray-300 px-3 text-xs font-medium text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
              >
                {t("view")}
              </Link>
            )}
          </div>
        </div>
      )}
    </li>
  );
}
