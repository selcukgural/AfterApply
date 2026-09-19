"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import type { BlogPostPublic } from "@/types/api";
import { adminBlogApi, blogApi } from "@/lib/api/blog";
import { ApiError } from "@/lib/api/httpClient";
import { useAuth } from "@/lib/auth/AuthContext";
import { blogPostPath, blogPreviewPath } from "@/lib/blog/blogPaths";
import { mediaSourcesIn, rewriteMediaSources } from "@/lib/blog/previewMedia";
import { SITE_URL } from "@/lib/seo/routes";
import { BlogArticle } from "./BlogArticle";

/**
 * The admin's look at a post before (or between) publishes. The article is `BlogArticle` — the
 * public page's component, unchanged — fed the draft from `/preview`, which the API shapes
 * exactly as it shapes the live post. The one thing added is the bar on top saying so; the one
 * thing altered is where a draft's images are fetched from (see `previewMedia`).
 */
export function BlogPreview({ postId }: { postId: string }) {
  const t = useTranslations("blog.preview");
  const locale = useLocale();
  const router = useRouter();
  const { isAuthenticated, isLoading } = useAuth();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      router.replace(`/login?next=${encodeURIComponent(blogPreviewPath(postId))}`);
    }
  }, [isLoading, isAuthenticated, router, postId]);

  const query = useQuery({
    queryKey: ["admin", "blog", "preview", postId],
    queryFn: () => adminBlogApi.preview(postId),
    enabled: isAuthenticated,
    retry: (failureCount, err) => !(err instanceof ApiError && (err.status === 403 || err.status === 404)) && failureCount < 2,
    // What is being previewed is what was saved when the tab opened; the editor opens a new tab
    // per preview, so a refetch would only move the page under the reader.
    staleTime: Number.POSITIVE_INFINITY,
    refetchOnWindowFocus: false,
  });

  // The post lives in one language, and the public page is under that locale: a preview opened
  // under the other one is moved, so the chrome around the article is the right one too.
  useEffect(() => {
    if (query.data && query.data.language !== locale) {
      router.replace(blogPreviewPath(postId), { locale: query.data.language });
    }
  }, [query.data, locale, router, postId]);

  const post = useDraftImages(query.data ?? null);

  if (isLoading || (isAuthenticated && query.isPending)) {
    return <p className="px-4 py-12 text-center text-sm text-gray-500 dark:text-gray-400">{t("loading")}</p>;
  }

  if (!isAuthenticated) {
    return null;
  }

  if (query.error || !post) {
    const forbidden = query.error instanceof ApiError && query.error.status === 403;
    return (
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-3 px-4 py-12">
        <p role="alert" className="text-sm text-red-600 dark:text-red-400">
          {forbidden ? t("forbidden") : t("notFound")}
        </p>
        <Link href="/admin/blog" className="text-sm font-medium text-blue-600 dark:text-blue-400">
          {t("backToAdmin")}
        </Link>
      </div>
    );
  }

  const path = blogPostPath(post.slug);
  const url = `${SITE_URL}/${post.language}${path}`;

  return (
    <>
      <div
        role="status"
        className="border-b border-warn/40 bg-warn-wash px-4 py-2 text-center text-xs text-warn-ink"
      >
        <span className="font-semibold">{t("badge")}</span>{" "}
        {post.slug ? t("address", { url }) : t("noAddressYet")}{" "}
        <Link href={`/admin/blog/${postId}`} className="font-medium underline underline-offset-2">
          {t("backToEditor")}
        </Link>
      </div>
      <BlogArticle post={post} url={url} inert />
    </>
  );
}

/**
 * The draft's images, reachable. Each `/api/blog/media/{id}` in the body and the cover is
 * fetched with the token and swapped for a blob URL; the blobs are revoked when the preview
 * goes away. Until the fetches land the post is shown with its original addresses, which for an
 * unpublished post means the image slots are empty for a moment rather than the whole page.
 */
function useDraftImages(post: BlogPostPublic | null): BlogPostPublic | null {
  const [resolved, setResolved] = useState<{ postId: string; sources: ReadonlyMap<string, string> } | null>(null);

  useEffect(() => {
    if (!post) return;
    const sources = mediaSourcesIn(post.contentHtml);
    if (post.coverImageUrl && !sources.includes(post.coverImageUrl)) sources.push(post.coverImageUrl);
    if (sources.length === 0) return;

    let cancelled = false;
    const blobs: string[] = [];
    void Promise.all(
      sources.map(async (source) => {
        try {
          const blob = URL.createObjectURL(await blogApi.fetchMediaBlob(source));
          blobs.push(blob);
          return [source, blob] as const;
        } catch {
          return null;
        }
      }),
    ).then((pairs) => {
      if (cancelled) return;
      setResolved({ postId: post.id, sources: new Map(pairs.filter((pair) => pair !== null)) });
    });

    return () => {
      cancelled = true;
      for (const blob of blobs) URL.revokeObjectURL(blob);
    };
  }, [post]);

  if (!post) return null;
  if (!resolved || resolved.postId !== post.id) return post;
  return {
    ...post,
    contentHtml: rewriteMediaSources(post.contentHtml, resolved.sources),
    coverImageUrl: post.coverImageUrl ? (resolved.sources.get(post.coverImageUrl) ?? post.coverImageUrl) : null,
  };
}
