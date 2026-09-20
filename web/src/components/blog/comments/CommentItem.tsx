"use client";

import { useEffect, useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { BlogComment, BlogCommentReportReason } from "@/types/api";
import { blogCommentsApi } from "@/lib/api/blogComments";
import { ApiError } from "@/lib/api/httpClient";
import { relativeTime } from "@/lib/blog/commentDraft";
import { CommentForm } from "./CommentForm";
import { ReportCommentDialog } from "./ReportCommentDialog";

interface CommentItemProps {
  comment: BlogComment;
  /** The root this one belongs to when it is a reply — where a reply to it goes. */
  rootId: string;
  isAuthenticated: boolean;
  signInHref: string;
  now: Date;
  onReplied: (rootId: string, reply: BlogComment) => void;
  onEdited: (comment: BlogComment) => void;
}

function initialsOf(name: string | null): string {
  if (!name) return "?";
  return name
    .split(" ")
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]!.toLocaleUpperCase("tr"))
    .join("");
}

/**
 * One comment: who, when, the text as plain text (React escapes it — a comment is never markup),
 * and the actions. The author's own pending comment is boxed and editable; everyone else's carries
 * "helpful", "reply" and the report menu. Replies render through the same component one level in.
 */
export function CommentItem({ comment, rootId, isAuthenticated, signInHref, now, onReplied, onEdited }: CommentItemProps) {
  const t = useTranslations("blogComments");
  const locale = useLocale();
  const [replying, setReplying] = useState(false);
  const [editing, setEditing] = useState(false);
  const [helpful, setHelpful] = useState(comment.helpfulByMe === true);
  const [helpfulCount, setHelpfulCount] = useState(comment.helpfulCount);
  const [helpfulBusy, setHelpfulBusy] = useState(false);
  const [menuOpen, setMenuOpen] = useState(false);
  const [reporting, setReporting] = useState(false);
  const [reportError, setReportError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const menuRef = useRef<HTMLDivElement>(null);

  // The list is read again when the viewer signs in; the vote state follows the fresh data
  // (the "adjust state on a prop change" pattern — a render-time reset, not an effect).
  const [seen, setSeen] = useState({ byMe: comment.helpfulByMe, count: comment.helpfulCount });
  if (seen.byMe !== comment.helpfulByMe || seen.count !== comment.helpfulCount) {
    setSeen({ byMe: comment.helpfulByMe, count: comment.helpfulCount });
    setHelpful(comment.helpfulByMe === true);
    setHelpfulCount(comment.helpfulCount);
  }

  useEffect(() => {
    if (!menuOpen) return;
    const close = (e: MouseEvent) => {
      if (menuRef.current && !menuRef.current.contains(e.target as Node)) setMenuOpen(false);
    };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [menuOpen]);

  const pending = comment.status === "Pending";
  const name = comment.isMine ? t("you") : (comment.authorName ?? t("anonymousAuthor"));
  const replyTarget = comment.authorName ?? t("anonymousAuthor");

  const toggleHelpful = async () => {
    if (helpfulBusy) return;
    setHelpfulBusy(true);
    const wasHelpful = helpful;
    const wasCount = helpfulCount;
    setHelpful(!wasHelpful);
    setHelpfulCount(wasCount + (wasHelpful ? -1 : 1));
    try {
      const result = await blogCommentsApi.toggleHelpful(comment.id);
      setHelpful(result.helpful);
      setHelpfulCount(result.helpfulCount);
    } catch {
      setHelpful(wasHelpful);
      setHelpfulCount(wasCount);
    } finally {
      setHelpfulBusy(false);
    }
  };

  const report = async (reason: BlogCommentReportReason, note: string | null) => {
    setReportError(null);
    try {
      const result = await blogCommentsApi.report(comment.id, { reason, note });
      setReporting(false);
      setNotice(result.alreadyReported ? t("reportDialog.already") : t("reportDialog.thanks"));
    } catch (err) {
      setReportError(err instanceof ApiError && err.message ? err.message : t("errors.generic"));
      throw err;
    }
  };

  const pillClass = (on: boolean) =>
    `inline-flex h-8 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition-colors disabled:opacity-60 ${
      on
        ? "border-accent bg-accent/10 text-accent-ink"
        : "border-gray-300 text-gray-700 hover:border-gray-400 dark:border-gray-700 dark:text-gray-300 dark:hover:border-gray-500"
    }`;
  const textButtonClass = "inline-flex h-8 items-center px-2 text-xs font-medium text-gray-700 hover:text-gray-900 dark:text-gray-300 dark:hover:text-gray-100";

  const body = (
    <>
      <div className="flex items-center justify-between gap-3">
        <div className="flex items-center gap-2.5">
          <div
            aria-hidden="true"
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-muted-wash text-xs font-semibold text-muted-ink"
          >
            {initialsOf(comment.authorName)}
          </div>
          <div className="flex flex-col">
            <span className="text-sm font-semibold text-gray-900 dark:text-gray-100">{name}</span>
            <span className="text-xs text-gray-500 dark:text-gray-400">
              <time dateTime={comment.createdAt}>{relativeTime(comment.createdAt, locale, now)}</time>
              {comment.editedAt && <> · {t("edited")}</>}
            </span>
          </div>
        </div>
        {pending && (
          <span className="rounded-full bg-warn-wash px-2 py-0.5 text-[11px] font-medium text-warn-ink">{t("pendingBadge")}</span>
        )}
      </div>

      {editing ? (
        <CommentForm
          label={t("editLabel")}
          placeholder={t("placeholder")}
          submitLabel={t("save")}
          initialValue={comment.content}
          autoFocus
          onCancel={() => setEditing(false)}
          onSubmit={async (content) => {
            const edited = await blogCommentsApi.edit(comment.id, { content });
            onEdited(edited);
            setEditing(false);
          }}
        />
      ) : (
        <p className="whitespace-pre-wrap break-words text-[15px] leading-relaxed text-gray-900 dark:text-gray-100">{comment.content}</p>
      )}

      {pending && !editing && (
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("pendingHint")}</p>
          <button type="button" className={textButtonClass} onClick={() => setEditing(true)}>
            {t("edit")}
          </button>
        </div>
      )}

      {!pending && (
        <div className="flex flex-wrap items-center gap-1" ref={menuRef}>
          {isAuthenticated ? (
            <button
              type="button"
              className={pillClass(helpful)}
              aria-pressed={helpful}
              aria-label={t("helpfulCount", { count: helpfulCount })}
              disabled={helpfulBusy}
              onClick={() => void toggleHelpful()}
            >
              <ThumbIcon />
              {t("helpful")}
              <span className="tabular-nums opacity-80">{helpfulCount}</span>
            </button>
          ) : (
            <Link href={signInHref} className={pillClass(false)} title={t("signInToVote")}>
              <ThumbIcon />
              {t("helpful")}
              <span className="tabular-nums opacity-80">{helpfulCount}</span>
            </Link>
          )}
          {isAuthenticated ? (
            <button type="button" className={textButtonClass} onClick={() => setReplying((open) => !open)}>
              {t("reply")}
            </button>
          ) : (
            <Link href={signInHref} className={textButtonClass}>
              {t("reply")}
            </Link>
          )}
          {isAuthenticated && !comment.isMine && (
            <div className="relative">
              <button
                type="button"
                className={`${textButtonClass} px-1.5 text-gray-500`}
                aria-label={t("more")}
                aria-haspopup="menu"
                aria-expanded={menuOpen}
                onClick={() => setMenuOpen((open) => !open)}
              >
                <DotsIcon />
              </button>
              {menuOpen && (
                <div
                  role="menu"
                  className="absolute left-0 top-9 z-10 min-w-[9rem] rounded-lg border border-gray-200 bg-white p-1 shadow-lg dark:border-gray-800 dark:bg-gray-900"
                >
                  <button
                    type="button"
                    role="menuitem"
                    className="block w-full rounded-md px-3 py-2 text-left text-sm text-gray-900 hover:bg-gray-100 dark:text-gray-100 dark:hover:bg-gray-800"
                    onClick={() => {
                      setMenuOpen(false);
                      setReporting(true);
                    }}
                  >
                    {t("report")}
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {notice && (
        <p role="status" className="text-xs text-gray-500 dark:text-gray-400">
          {notice}
        </p>
      )}

      {replying && (
        <div className="mt-1">
          <CommentForm
            label={t("replyLabel")}
            placeholder={t("replyPlaceholder", { name: replyTarget })}
            submitLabel={t("submit")}
            autoFocus
            onCancel={() => setReplying(false)}
            onSubmit={async (content) => {
              const reply = await blogCommentsApi.reply(comment.id, { content });
              onReplied(rootId, reply);
              setReplying(false);
            }}
          />
        </div>
      )}

      {reporting && <ReportCommentDialog onClose={() => setReporting(false)} onSubmit={report} error={reportError} />}
    </>
  );

  return (
    <article
      className={`flex flex-col gap-2.5 ${
        pending ? "rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-3.5 dark:border-gray-700 dark:bg-gray-900/60" : ""
      }`}
    >
      {body}
      {comment.replies.length > 0 && (
        <div className="ml-3 mt-2 flex flex-col gap-4 border-l-2 border-gray-200 pl-3 sm:ml-5 sm:pl-5 dark:border-gray-800">
          {comment.replies.map((reply) => (
            <CommentItem
              key={reply.id}
              comment={reply}
              rootId={rootId}
              isAuthenticated={isAuthenticated}
              signInHref={signInHref}
              now={now}
              onReplied={onReplied}
              onEdited={onEdited}
            />
          ))}
        </div>
      )}
    </article>
  );
}

function ThumbIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M7 10v12" />
      <path d="M15 5.88 14 10h5.83a2 2 0 0 1 1.92 2.56l-2.33 8A2 2 0 0 1 17.5 22H4a2 2 0 0 1-2-2v-8a2 2 0 0 1 2-2h2.76a2 2 0 0 0 1.79-1.11L12 2a3.13 3.13 0 0 1 3 3.88Z" />
    </svg>
  );
}

function DotsIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4" fill="currentColor" aria-hidden="true">
      <circle cx="5" cy="12" r="1.8" />
      <circle cx="12" cy="12" r="1.8" />
      <circle cx="19" cy="12" r="1.8" />
    </svg>
  );
}
