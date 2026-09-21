import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { BlogPostPublic } from "@/types/api";
import { BLOG_PATH, blogPostPath } from "@/lib/blog/blogPaths";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";
import { ShareRow } from "@/components/share/ShareRow";
import { BlogArticleBody } from "@/components/blog/BlogArticleBody";
import { LikeButton } from "@/components/blog/LikeButton";
import { CommentSection } from "@/components/blog/comments/CommentSection";
import type { BlogCommentList } from "@/types/api";

interface BlogArticleProps {
  post: BlogPostPublic;
  /** The absolute address of the post — what the share row hands out. */
  url: string;
  /**
   * The admin's preview (2026-09-20): the very same markup, but the footer's controls do not
   * act — a like on an unpublished post would 404, and a share would pass on a link that is not
   * live yet. The `inert` attribute keeps them visible and untouchable.
   */
  inert?: boolean;
  /**
   * The comments under the post (2026-09-20): the anonymous first page fetched with the post, or
   * null when the API could not answer (the section then loads in the browser). Left out
   * entirely by the preview — a draft has no comments and nothing to write one on.
   */
  comments?: BlogCommentList | null;
  /** The server's clock at render, for the comments' relative times (see `CommentSection`). */
  renderedAt?: string;
}

/**
 * One post as the public page shows it — header, cover, body, like and share — and nothing
 * else. Kept as one component on purpose: the editor's preview renders this with the draft in
 * place of the published version, so a preview can never differ from the page it stands for
 * except by data. Sync (no `async`), so it renders inside both a server page and the preview's
 * client tree; `useTranslations` and `useLocale` work in both.
 */
export function BlogArticle({ post, url, inert = false, comments, renderedAt }: BlogArticleProps) {
  const locale = useLocale();
  const t = useTranslations("blog");
  const tSection = useTranslations("metadata.pages");
  const path = blogPostPath(post.slug);
  const updated = post.updatedAt.slice(0, 10) !== post.publishedAt.slice(0, 10);

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <Link href={BLOG_PATH} className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {tSection("blog.title")}
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{post.title}</h1>
        {post.excerpt && <p className="text-lg leading-7 text-gray-600 dark:text-gray-400">{post.excerpt}</p>}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500 dark:text-gray-500">
          <time dateTime={post.publishedAt}>{t("publishedOn", { date: formatArticleDate(post.publishedAt.slice(0, 10), locale) })}</time>
          {updated && (
            <time dateTime={post.updatedAt}>{t("updatedOn", { date: formatArticleDate(post.updatedAt.slice(0, 10), locale) })}</time>
          )}
          {post.translation && (
            <Link
              href={blogPostPath(post.translation.slug)}
              locale={post.translation.language}
              hrefLang={post.translation.language}
              className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400"
            >
              {t("readInOtherLanguage", { language: post.translation.language })}
            </Link>
          )}
        </div>
        {post.coverImageUrl && (
          <img
            src={post.coverImageUrl}
            alt={post.coverAlt ?? ""}
            className="mt-2 w-full rounded-lg border border-gray-200 object-cover dark:border-gray-800"
          />
        )}
      </header>

      <div className="border-t border-gray-200 pt-6 dark:border-gray-800">
        <BlogArticleBody html={post.contentHtml} lang={post.language} />
      </div>

      <footer className="flex flex-col gap-4 border-t border-gray-200 pt-6 dark:border-gray-800" inert={inert || undefined}>
        <div className="flex flex-wrap items-center gap-4">
          <LikeButton
            postId={post.id}
            initialCount={post.likeCount}
            initialLiked={post.likedByMe}
            signInHref={`/login?next=${encodeURIComponent(path)}`}
          />
          <ShareRow label={t("share")} content={{ text: post.title, url }} />
          {/* How many times the post has been opened (2026-09-20): the API's plain tally, bumped
              by this very fetch, so a reader is always at least the first. */}
          <span className="inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
            <svg viewBox="0 0 24 24" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
              <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 12s3.75-6.75 9.75-6.75S21.75 12 21.75 12s-3.75 6.75-9.75 6.75S2.25 12 2.25 12Z" />
              <circle cx="12" cy="12" r="3" />
            </svg>
            <span className="tabular-nums">{t("viewCount", { count: post.viewCount })}</span>
          </span>
        </div>
        <Link href={BLOG_PATH} className="text-sm font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400">
          {t("backToList")}
        </Link>
      </footer>

      {comments !== undefined && (
        <CommentSection
          postId={post.id}
          initial={comments}
          signInHref={`/login?next=${encodeURIComponent(path)}`}
          renderedAt={renderedAt ?? new Date().toISOString()}
        />
      )}
    </div>
  );
}
