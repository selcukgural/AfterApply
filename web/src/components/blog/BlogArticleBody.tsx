import type { BlogLanguage } from "@/types/api";

interface BlogArticleBodyProps {
  html: string;
  lang: BlogLanguage;
}

/**
 * The post's body, rendered from HTML the API stored.
 *
 * This is the site's second `dangerouslySetInnerHTML` on content (the first is the job
 * description card, sanitized with DOMPurify in the browser). It is safe on a different basis:
 * the HTML never comes from a user or a scraped page — it is written by an admin in the editor,
 * passed through the API's allowlist sanitizer on *every* save (`BlogHtmlSanitizer`: tags,
 * attributes, five CSS properties, http(s) only, images only from our own media route, external
 * links hardened) and stored already clean. What arrives here is what that sanitizer produced;
 * the browser never assembles or re-sanitizes it. A server component on purpose — the markup is
 * in the HTML for crawlers and for readers with no account, and there is nothing to hydrate.
 *
 * `blog.contract.test.ts` pins the set of files allowed to use the attribute.
 */
export function BlogArticleBody({ html, lang }: BlogArticleBodyProps) {
  return <div className="blog-prose" lang={lang} dangerouslySetInnerHTML={{ __html: html }} />;
}
