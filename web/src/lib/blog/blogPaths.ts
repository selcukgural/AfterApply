import { routing } from "@/i18n/routing";
import type { BlogLanguage, BlogPostKind, BlogTranslationLink } from "@/types/api";

export const BLOG_PATH = "/blog";

/** Where the API serves a post's images, as the stored HTML refers to them (relative, id only). */
export const BLOG_MEDIA_PATH_PREFIX = "/api/blog/media/";

/** The editor's preview of one post, under the post's own language. */
export function blogPreviewPath(postId: string): string {
  return `${BLOG_PATH}/preview/${postId}`;
}

export function blogPostPath(slug: string): string {
  return `${BLOG_PATH}/${slug}`;
}

/**
 * The guide is written in the blog's editor (2026-09-26); these are the few places the kind
 * decides the address. `/guide` is spelled here rather than imported from `lib/guide` so the
 * editor's bundle does not pull in the guide's article list.
 */
const SECTION_PATH: Record<BlogPostKind, string> = { Blog: BLOG_PATH, Guide: "/guide" };

const ADMIN_PATH: Record<BlogPostKind, string> = { Blog: "/admin/blog", Guide: "/admin/guide" };

/** A published post's page: `/blog/<slug>` or `/guide/<slug>`. */
export function postPath(kind: BlogPostKind, slug: string): string {
  return `${SECTION_PATH[kind]}/${slug}`;
}

/** The editor's preview of one post, under its own section and (by the caller) language. */
export function postPreviewPath(kind: BlogPostKind, postId: string): string {
  return `${SECTION_PATH[kind]}/preview/${postId}`;
}

/** The admin table of one kind; the editor lives under it (`/admin/guide/<id>`). */
export function adminPostsPath(kind: BlogPostKind): string {
  return ADMIN_PATH[kind];
}

/** What a post page needs to say where its other-language twin lives, if anywhere. */
export interface BlogAlternatesInput {
  language: BlogLanguage;
  slug: string;
  translation: BlogTranslationLink | null;
}

/**
 * The hreflang set for one post. Only the locales that actually have a page: a post lives in one
 * language, so the site-wide `alternateLanguages` — which always names both locales — would send
 * a crawler to a 404 for the missing one. `x-default` is the Turkish page when there is one
 * (the site's default locale), else the post's own.
 */
export function blogAlternates(post: BlogAlternatesInput, base = ""): Record<string, string> {
  return postAlternates("Blog", post, base);
}

/** The same for either section: a guide's pair lives under `/guide` (2026-09-26). */
export function postAlternates(kind: BlogPostKind, post: BlogAlternatesInput, base = ""): Record<string, string> {
  const pages: Record<string, string> = {
    [post.language]: `${base}/${post.language}${postPath(kind, post.slug)}`,
  };
  if (post.translation) {
    pages[post.translation.language] = `${base}/${post.translation.language}${postPath(kind, post.translation.slug)}`;
  }
  const xDefault = pages[routing.defaultLocale] ?? pages[post.language];
  return { ...pages, "x-default": xDefault };
}
