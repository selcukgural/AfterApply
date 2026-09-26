import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { BlogPostPublic } from "@/types/api";
import { postPath } from "@/lib/blog/blogPaths";
import { formatArticleDate } from "@/lib/guide/formatArticleDate";
import { ShareRow } from "@/components/share/ShareRow";
import { BlogArticleBody } from "@/components/blog/BlogArticleBody";
import { LikeButton } from "@/components/blog/LikeButton";

interface GuideArticleProps {
  post: BlogPostPublic;
  /** The absolute address of the guide — what the share row hands out. */
  url: string;
  /** The admin's preview: the same markup, with the footer's controls untouchable (see `BlogArticle`). */
  inert?: boolean;
}

/**
 * One guide as its page shows it (2026-09-26): the guide page's own layout — header, body, the
 * sign-up box, related guides — with the blog's like button, share row and view tally in the
 * footer, and no comments (DECISIONS.md 2026-09-26). The body is the blog's: the same sanitized
 * HTML from the same editor, rendered by the same server component. Like `BlogArticle`, one
 * component for the page and the editor's preview, so the two can differ only by data.
 */
export function GuideArticle({ post, url, inert = false }: GuideArticleProps) {
  const locale = useLocale();
  const tBlog = useTranslations("blog");
  const t = useTranslations("guide.article");
  const tSection = useTranslations("metadata.pages");
  const path = postPath("Guide", post.slug);
  const updated = post.updatedAt.slice(0, 10) !== post.publishedAt.slice(0, 10);

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-10 px-4 py-12">
      <header className="flex flex-col gap-3">
        <Link href="/guide" className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {tSection("guide.title")}
        </Link>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">{post.title}</h1>
        {post.excerpt && <p className="text-lg leading-7 text-gray-600 dark:text-gray-400">{post.excerpt}</p>}
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-gray-500 dark:text-gray-500">
          {updated ? (
            <time dateTime={post.updatedAt}>{t("updatedOn", { date: formatArticleDate(post.updatedAt.slice(0, 10), locale) })}</time>
          ) : (
            <time dateTime={post.publishedAt}>{t("publishedOn", { date: formatArticleDate(post.publishedAt.slice(0, 10), locale) })}</time>
          )}
          {post.translation && (
            <Link
              href={postPath("Guide", post.translation.slug)}
              locale={post.translation.language}
              hrefLang={post.translation.language}
              className="font-medium text-blue-600 underline-offset-2 hover:underline dark:text-blue-400"
            >
              {tBlog("readInOtherLanguage", { language: post.translation.language })}
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

      <div className="border-t border-gray-200 pt-2 dark:border-gray-800">
        <BlogArticleBody html={post.contentHtml} lang={post.language} />
      </div>

      <footer className="flex flex-wrap items-center gap-4 border-t border-gray-200 pt-6 dark:border-gray-800" inert={inert || undefined}>
        <LikeButton
          postId={post.id}
          initialCount={post.likeCount}
          initialLiked={post.likedByMe}
          signInHref={`/login?next=${encodeURIComponent(path)}`}
        />
        <ShareRow label={tBlog("share")} content={{ text: post.title, url }} />
        <span className="inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
          <svg viewBox="0 0 24 24" className="h-3.5 w-3.5" fill="none" stroke="currentColor" strokeWidth={2} aria-hidden="true">
            <path strokeLinecap="round" strokeLinejoin="round" d="M2.25 12s3.75-6.75 9.75-6.75S21.75 12 21.75 12s-3.75 6.75-9.75 6.75S2.25 12 2.25 12Z" />
            <circle cx="12" cy="12" r="3" />
          </svg>
          <span className="tabular-nums">{tBlog("viewCount", { count: post.viewCount })}</span>
        </span>
      </footer>

      {!post.hideRegisterCta && (
        <section className="rounded-lg border border-blue-200 bg-blue-50/60 p-6 dark:border-blue-900 dark:bg-blue-950/30" inert={inert || undefined}>
          <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("cta.title")}</h2>
          <p className="mt-2 text-sm leading-6 text-gray-700 dark:text-gray-300">{t("cta.body")}</p>
          <Link
            href="/register"
            className="mt-4 inline-flex items-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            {t("cta.button")}
          </Link>
        </section>
      )}

      {post.related.length > 0 && (
        <section className="flex flex-col gap-3 border-t border-gray-200 pt-8 dark:border-gray-800">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("related")}</h2>
          <ul className="flex flex-col gap-2">
            {post.related.map((entry) => (
              <li key={entry.slug}>
                <Link
                  href={postPath("Guide", entry.slug)}
                  className="text-sm font-medium text-blue-600 underline underline-offset-2 dark:text-blue-400"
                >
                  {entry.title}
                </Link>
              </li>
            ))}
          </ul>
        </section>
      )}
    </div>
  );
}
