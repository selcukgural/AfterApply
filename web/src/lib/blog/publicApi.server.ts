// Server-side only by convention (called from server components and the sitemap); it holds no
// secret, so the `server-only` guard package is not pulled in for it.
import type { BlogCommentList, BlogLanguage, BlogPostListItem, BlogPostPublic, BlogSlug, PagedResult } from "@/types/api";

const API_BASE_URL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5151";

/**
 * The same two kinds of fetch as the company pages (`lib/companies/publicApi.server.ts`): the
 * list and the post are read fresh on every request — the API caches them in Redis and evicts on
 * every publish, whereas Next's own data cache is per web container and would hold a stale page
 * a minute past an unpublish — and the sitemap's slug list stays on a one-minute revalidate.
 */
const REVALIDATE_SECONDS = 60;

type Freshness = "fresh" | "revalidate";

async function fetchPublic<T>(path: string, locale: string, freshness: Freshness): Promise<T | null> {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    headers: { Accept: "application/json", "Accept-Language": locale },
    ...(freshness === "fresh" ? { cache: "no-store" } : { next: { revalidate: REVALIDATE_SECONDS } }),
  });
  if (response.status === 404) {
    return null;
  }
  if (!response.ok) {
    throw new Error(`Public blog API answered ${response.status} for ${path}`);
  }
  return (await response.json()) as T;
}

/** Null when the blog is switched off (404) — the page then does not exist either. */
export function fetchBlogList(language: BlogLanguage, page: number): Promise<PagedResult<BlogPostListItem> | null> {
  return fetchPublic<PagedResult<BlogPostListItem>>(`/api/blog/public/posts?lang=${language}&page=${page}`, language, "fresh");
}

export function fetchBlogPost(language: BlogLanguage, slug: string): Promise<BlogPostPublic | null> {
  return fetchPublic<BlogPostPublic>(`/api/blog/public/posts/${language}/${encodeURIComponent(slug)}`, language, "fresh");
}

/**
 * The anonymous first page of a post's comments (2026-09-20), rendered with the post so a reader
 * — and a crawler — gets the approved comments in the HTML. The signed-in view (own pending
 * comments, helpful votes) is fetched again in the browser with the token. Null when the API
 * cannot answer: the article still renders, the section then loads client-side.
 */
export async function fetchBlogComments(postId: string, locale: string): Promise<BlogCommentList | null> {
  try {
    return await fetchPublic<BlogCommentList>(`/api/blog/public/posts/${postId}/comments`, locale, "fresh");
  } catch {
    return null;
  }
}

/** Empty when the API is unreachable or the blog is off: the sitemap degrades, never fails. */
export async function fetchBlogSlugs(): Promise<BlogSlug[]> {
  try {
    return (await fetchPublic<BlogSlug[]>("/api/blog/public/slugs", "tr", "revalidate")) ?? [];
  } catch {
    return [];
  }
}
