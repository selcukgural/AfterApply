import { routing } from "@/i18n/routing";
import type { BlogLanguage, BlogTranslationLink } from "@/types/api";

export const BLOG_PATH = "/blog";

export function blogPostPath(slug: string): string {
  return `${BLOG_PATH}/${slug}`;
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
  const pages: Record<string, string> = {
    [post.language]: `${base}/${post.language}${blogPostPath(post.slug)}`,
  };
  if (post.translation) {
    pages[post.translation.language] = `${base}/${post.translation.language}${blogPostPath(post.translation.slug)}`;
  }
  const xDefault = pages[routing.defaultLocale] ?? pages[post.language];
  return { ...pages, "x-default": xDefault };
}
