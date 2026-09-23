"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { BlogComment, BlogCommentList } from "@/types/api";
import { blogCommentsApi } from "@/lib/api/blogComments";
import { useAuth } from "@/lib/auth/AuthContext";
import { buttonClassName } from "@/components/ui/Button";
import { CommentForm } from "./CommentForm";
import { CommentItem } from "./CommentItem";

interface CommentSectionProps {
  postId: string;
  /** The anonymous first page, rendered with the post; null when the API could not answer. */
  initial: BlogCommentList | null;
  /** Where the sign-in prompts point — back here afterwards. */
  signInHref: string;
  /** The server's clock at render, so every "2 hours ago" hydrates to the same text it was sent as. */
  renderedAt: string;
}

/**
 * The comments under a post (2026-09-20). The first page arrives server-rendered and anonymous;
 * once the viewer is known to be signed in the page is fetched again with their token, which is
 * what adds their own pending comments and their helpful votes. "Load more" appends pages; a new
 * comment, reply or edit is patched into what is on screen rather than refetched.
 */
export function CommentSection({ postId, initial, signInHref, renderedAt }: CommentSectionProps) {
  const t = useTranslations("blogComments");
  const { isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const [submitted, setSubmitted] = useState(false);
  const [more, setMore] = useState<{ pages: BlogCommentList[]; loading: boolean; failed: boolean }>({ pages: [], loading: false, failed: false });
  const now = new Date(renderedAt);

  // The anonymous first page is what the server sent; a signed-in reader's differs (own pending
  // comments, votes), so it is read again as them — a different key, a fresh request.
  const firstKey = ["blog", "comments", postId, { signedIn: isAuthenticated }] as const;
  const first = useQuery({
    queryKey: firstKey,
    queryFn: () => blogCommentsApi.list(postId, 1),
    enabled: isAuthenticated || initial === null,
    initialData: !isAuthenticated && initial ? initial : undefined,
    staleTime: Number.POSITIVE_INFINITY,
    refetchOnWindowFocus: false,
  });

  const pages = first.data ? [first.data, ...more.pages] : [];
  const items = pages.flatMap((page) => page.items);
  const totalCount = first.data?.totalCount ?? 0;
  const loading = first.isLoading || more.loading;
  const loadError = first.isError || more.failed;
  const hasMore = first.data !== undefined && items.length < first.data.totalRootCount;

  const loadMore = async () => {
    setMore((current) => ({ ...current, loading: true, failed: false }));
    try {
      const page = await blogCommentsApi.list(postId, pages.length + 1);
      setMore((current) => ({ pages: [...current.pages, page], loading: false, failed: false }));
    } catch {
      setMore((current) => ({ ...current, loading: false, failed: true }));
    }
  };

  // What the reader just did is patched into what is on screen, not refetched.
  const patchFirst = (update: (page: BlogCommentList) => BlogCommentList) =>
    queryClient.setQueryData<BlogCommentList>(firstKey, (page) => (page ? update(page) : page));
  const patchAll = (update: (comment: BlogComment) => BlogComment) => {
    const patchPage = (page: BlogCommentList) => ({ ...page, items: page.items.map((root) => update({ ...root, replies: root.replies.map(update) })) });
    patchFirst(patchPage);
    setMore((current) => ({ ...current, pages: current.pages.map(patchPage) }));
  };

  const prepend = (comment: BlogComment) =>
    patchFirst((page) => ({
      ...page,
      items: [comment, ...page.items],
      totalRootCount: page.totalRootCount + 1,
      totalCount: page.totalCount + (comment.status === "Approved" ? 1 : 0),
    }));

  const onReplied = (rootId: string, reply: BlogComment) => {
    patchAll((comment) => (comment.id === rootId ? { ...comment, replies: [...comment.replies, reply] } : comment));
    if (reply.status === "Approved") patchFirst((page) => ({ ...page, totalCount: page.totalCount + 1 }));
  };

  const onEdited = (edited: BlogComment) =>
    patchAll((comment) => (comment.id === edited.id ? { ...comment, content: edited.content, editedAt: edited.editedAt } : comment));

  return (
    <section id="comments" className="flex scroll-mt-20 flex-col gap-8 border-t border-gray-200 pt-8 dark:border-gray-800" aria-labelledby="comments-heading">
      <div className="flex flex-col gap-3">
        <h2 id="comments-heading" className="text-lg font-semibold text-gray-900 dark:text-gray-100">
          {t("heading")}
        </h2>
        <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("intro")}</p>
        {isAuthenticated ? (
          <CommentForm
            label={t("label")}
            placeholder={t("placeholder")}
            submitLabel={t("submit")}
            note={
              <>
                {t("policy")} {t("nameNote")}
              </>
            }
            onSubmit={async (content) => {
              const comment = await blogCommentsApi.create(postId, { content });
              prepend(comment);
              setSubmitted(comment.status === "Pending");
            }}
          />
        ) : (
          <div className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-gray-200 bg-gray-50 p-4 dark:border-gray-800 dark:bg-gray-900/60">
            <p className="text-sm text-gray-700 dark:text-gray-300">{t("signInPrompt")}</p>
            <Link href={signInHref} className={buttonClassName("outline")}>
              {t("signIn")}
            </Link>
          </div>
        )}
        {submitted && (
          <p role="status" className="rounded-lg bg-good-wash px-3 py-2 text-sm text-good-ink">
            {t("submittedPending")}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-6">
        {items.length > 0 && (
          <h3 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("listTitle", { count: totalCount })}</h3>
        )}

        {items.length === 0 && !loading && !loadError && (
          <div className="flex flex-col gap-1 rounded-lg border border-dashed border-gray-300 px-4 py-8 text-center dark:border-gray-700">
            <p className="text-[15px] font-medium text-gray-900 dark:text-gray-100">{t("emptyTitle")}</p>
            <p className="text-sm leading-6 text-gray-500 dark:text-gray-400">{t("emptyBody")}</p>
          </div>
        )}

        {items.map((comment) => (
          <CommentItem
            key={comment.id}
            comment={comment}
            rootId={comment.id}
            isAuthenticated={isAuthenticated}
            signInHref={signInHref}
            now={now}
            onReplied={onReplied}
            onEdited={onEdited}
          />
        ))}

        {loadError && (
          <p role="alert" className="text-sm text-red-600 dark:text-red-400">
            {t("loadError")}
          </p>
        )}
        {loading && <p className="text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>}
        {hasMore && !loading && (
          <button type="button" className={buttonClassName("outline", "w-full justify-center")} onClick={() => void loadMore()}>
            {t("loadMore")}
          </button>
        )}
      </div>
    </section>
  );
}
