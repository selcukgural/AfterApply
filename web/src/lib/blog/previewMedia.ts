import { BLOG_MEDIA_PATH_PREFIX } from "./blogPaths";

/**
 * The preview's one departure from the public page's data (2026-09-20): a draft's images. The
 * stored HTML points at `/api/blog/media/{id}`, which the API serves without a token only once
 * the post is published — before that the author's Bearer is required, and an `<img>` cannot
 * send one. So the preview fetches each image with the token and swaps the address for a blob
 * URL, leaving every other byte of the markup as it is. The sanitizer allows images from that
 * one route only, which is what makes a plain match on the attribute enough.
 */
const MEDIA_SRC = new RegExp(`src="(${BLOG_MEDIA_PATH_PREFIX}[0-9a-f-]{36})"`, "g");

/** Every media address the body refers to, once each, in order of first appearance. */
export function mediaSourcesIn(html: string): string[] {
  const found: string[] = [];
  for (const match of html.matchAll(MEDIA_SRC)) {
    if (!found.includes(match[1])) found.push(match[1]);
  }
  return found;
}

/** The body with each media address replaced by what `resolved` maps it to; an address with no
 *  mapping (a fetch that failed) is left alone, so the broken image shows as broken. */
export function rewriteMediaSources(html: string, resolved: ReadonlyMap<string, string>): string {
  return html.replace(MEDIA_SRC, (whole, src: string) => {
    const blob = resolved.get(src);
    return blob ? `src="${blob}"` : whole;
  });
}
