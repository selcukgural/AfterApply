/**
 * The editor's SEO section, as pure functions (DECISIONS.md 2026-09-21): the slug the server
 * would generate, the two length guides, what a search result shows, and the checklist. Nothing
 * here touches the DOM — the body is sanitized HTML and is read with plain string scans, so the
 * same code runs in a test, in the editor and on the server (JSON-LD's `wordCount`).
 */

/** Google states no length for either; these are what a result shows before cutting — a guide,
 *  not a cap (the store allows 120 and 500). */
export const SEO_TITLE_MAX = 60;
export const META_DESCRIPTION_MIN = 70;
export const META_DESCRIPTION_MAX = 160;
export const SECONDARY_KEYWORDS_MAX = 8;
/** The server's own cap on a slug (`BlogSlugGenerator.MaxLength`). */
export const SLUG_MAX_LENGTH = 100;

/** Same table as `BlogSlugGenerator` on the API: the four i's fold together first, then the rest. */
export function foldCase(value: string): string {
  return value.replace(/[İIı]/g, "i").toLowerCase();
}

const SLUG_LETTERS: Record<string, string> = { ç: "c", ğ: "g", ö: "o", ş: "s", ü: "u", â: "a", î: "i", û: "u" };
const RESERVED_SLUGS = new Set(["public", "new", "media", "admin", "feed", "rss", "page", "preview"]);

/**
 * The slug the server would generate for a title — so the editor can show it live before the
 * first publish instead of leaving the field empty until then. Mirrors `BlogSlugGenerator.Generate`
 * (the contract test on the API pins the two to the same outputs); uniqueness is the server's.
 */
export function slugFromTitle(title: string): string {
  const folded = foldCase(title.trim()).replace(/[çğöşüâîû]/g, (ch) => SLUG_LETTERS[ch] ?? ch);
  let slug = folded.replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
  if (slug.length > SLUG_MAX_LENGTH) slug = slug.slice(0, SLUG_MAX_LENGTH).replace(/-+$/g, "");
  if (slug.length === 0) slug = "post";
  return RESERVED_SLUGS.has(slug) ? `${slug}-2` : slug;
}

const ENTITIES: Record<string, string> = { "&nbsp;": " ", "&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": '"', "&#39;": "'", "&apos;": "'" };

/** The body as plain text: tags dropped, entities the sanitizer emits decoded, whitespace folded.
 *  Entities go in one pass, so "&amp;lt;" becomes "&lt;" and not "<" (a second decode would
 *  otherwise re-read what the first produced). */
export function textOfHtml(html: string): string {
  return html
    .replace(/<(script|style)[\s\S]*?<\/\1>/gi, " ")
    .replace(/<[^>]+>/g, " ")
    .replace(/&(?:nbsp|amp|lt|gt|quot|#39|apos);/g, (entity) => ENTITIES[entity] ?? entity)
    .replace(/\s+/g, " ")
    .trim();
}

/** Tokens with at least one letter or digit — a lone "&" or "—" is not a word. */
export function wordCount(html: string): number {
  const text = textOfHtml(html);
  return text.length === 0 ? 0 : text.split(" ").filter((token) => /[\p{L}\p{N}]/u.test(token)).length;
}

/** The text of the first paragraph that has any — where a reader (and a crawler) starts. */
export function firstParagraph(html: string): string {
  for (const match of html.matchAll(/<p\b[^>]*>([\s\S]*?)<\/p>/gi)) {
    const text = textOfHtml(match[1] ?? "");
    if (text.length > 0) return text;
  }
  return "";
}

/** The text of every h2 and h3 in the body, in order. */
export function headings(html: string): string[] {
  return [...html.matchAll(/<h[23]\b[^>]*>([\s\S]*?)<\/h[23]>/gi)].map((m) => textOfHtml(m[1] ?? "")).filter((t) => t.length > 0);
}

/** Every body image's alt, in order; "" for one without. */
export function imageAlts(html: string): string[] {
  return [...html.matchAll(/<img\b[^>]*>/gi)].map((m) => {
    const alt = /\balt\s*=\s*("([^"]*)"|'([^']*)')/i.exec(m[0]);
    return (alt?.[2] ?? alt?.[3] ?? "").trim();
  });
}

/** The words of a phrase, case-folded, punctuation dropped; "" gives none. */
function wordsOf(value: string): string[] {
  return foldCase(value)
    .split(/[^\p{L}\p{N}]+/u)
    .filter((w) => w.length > 0);
}

/**
 * Whether a keyword occurs in a text, the way an SEO checker counts it: the exact phrase, or
 * every word of it somewhere in the text — "işe alım ghosting" is in "İşe Alım Sürecinde
 * Ghosting" (Yoast's rule; a heading that separates the words with one more still targets the
 * phrase). Case-folded the Turkish way. Empty keyword: false.
 */
export function containsKeyword(text: string, keyword: string): boolean {
  const k = foldCase(keyword.trim());
  if (k.length === 0) return false;
  const folded = foldCase(text);
  if (folded.includes(k)) return true;
  const words = new Set(wordsOf(text));
  return wordsOf(keyword).every((w) => words.has(w));
}

export type SeoCheckStatus = "ok" | "warn" | "missing";

/**
 * The checklist's items (DECISIONS.md 2026-09-21): what Google Search Central documents, as the
 * base, plus the field's established on-page practices that Google does not contradict — each
 * item says which it is (`basis`) and links its source. What Google explicitly rules out is
 * not here: no word-count item (its helpful-content FAQ says there is no such requirement), no
 * keyword-density or repetition item (its spam policy calls that stuffing), no target number
 * of secondary keywords (no evidence for one).
 */
export type SeoCheckId =
  /** "Influencing your title links": descriptive, concise, no keyword stuffing. */
  | "title"
  /** "Control your snippets": a meta description that accurately sums up the page. */
  | "description"
  /** SEO Starter Guide, "Anticipate your readers' search terms": the words readers search for are in the text. */
  | "searchTerms"
  /** Practice: the main topic in the title, ideally first — Google's title guidance asks for a title that describes the page; practitioners put the topic up front. */
  | "keywordInTitle"
  /** "URL structure best practices": words relevant to the content in the URL. */
  | "keywordInUrl"
  /** Practice: the topic named in the opening paragraph, where a reader (and a snippet) starts. */
  | "keywordEarly"
  /** SEO Starter Guide, "Links": link to related pages with descriptive text; at least one link in the body. */
  | "links"
  /** SEO Starter Guide, "Use headings to emphasize important text" and "Organize your topics". */
  | "headings"
  /** "URL structure best practices": descriptive words, hyphens, not an id. */
  | "url"
  /** "Google Images SEO best practices": descriptive alt text for the cover. */
  | "coverAlt"
  /** The same, for the images in the body. */
  | "bodyImageAlts"
  /** "Article structured data": BlogPosting with headline, image, dates, author — emitted by the page. */
  | "structuredData";

export type SeoCheckBasis = "google" | "practice";

/** Where each item comes from: Google's own page, or the practice's best-known write-up. */
export const SEO_CHECK_SOURCES: Record<SeoCheckId, { basis: SeoCheckBasis; url: string }> = {
  title: { basis: "google", url: "https://developers.google.com/search/docs/appearance/title-link" },
  description: { basis: "google", url: "https://developers.google.com/search/docs/appearance/snippet" },
  searchTerms: { basis: "google", url: "https://developers.google.com/search/docs/fundamentals/seo-starter-guide" },
  keywordInTitle: { basis: "practice", url: "https://moz.com/learn/seo/title-tag" },
  keywordInUrl: { basis: "google", url: "https://developers.google.com/search/docs/crawling-indexing/url-structure" },
  keywordEarly: { basis: "practice", url: "https://moz.com/learn/seo/on-page-factors" },
  headings: { basis: "google", url: "https://developers.google.com/search/docs/fundamentals/seo-starter-guide" },
  links: { basis: "google", url: "https://developers.google.com/search/docs/fundamentals/seo-starter-guide" },
  url: { basis: "google", url: "https://developers.google.com/search/docs/crawling-indexing/url-structure" },
  coverAlt: { basis: "google", url: "https://developers.google.com/search/docs/appearance/google-images" },
  bodyImageAlts: { basis: "google", url: "https://developers.google.com/search/docs/appearance/google-images" },
  structuredData: { basis: "google", url: "https://developers.google.com/search/docs/appearance/structured-data/article" },
};

export interface SeoCheck {
  id: SeoCheckId;
  status: SeoCheckStatus;
  /** A number the label can show ("55 karakter", "3/5 görsel"). */
  value?: number;
}

export interface SeoInput {
  title: string;
  seoTitle: string;
  excerpt: string;
  slug: string;
  primaryKeyword: string;
  secondaryKeywords: string[];
  coverAlt: string;
  hasCover: boolean;
  contentHtml: string;
}

/** The title a search result would show: the override when set, else the headline. */
export function effectiveTitle(input: Pick<SeoInput, "title" | "seoTitle">): string {
  return input.seoTitle.trim() || input.title.trim();
}

/**
 * The checklist. Only items that apply are returned — a post without a cover is not asked for
 * a cover alt; a post without images in the body is not asked for their alts. The order is the
 * order the section shows them.
 */
export function seoChecklist(input: SeoInput): SeoCheck[] {
  const checks: SeoCheck[] = [];
  const title = effectiveTitle(input);
  const description = input.excerpt.trim();
  const keyword = input.primaryKeyword.trim();
  const text = `${input.title} ${textOfHtml(input.contentHtml)}`;

  // Google names no length; a title past what the result shows is cut, which is what "concise"
  // guards against. The number is the display width, not a rule.
  checks.push({
    id: "title",
    status: title.length === 0 ? "missing" : title.length <= SEO_TITLE_MAX ? "ok" : "warn",
    value: title.length,
  });
  checks.push({
    id: "description",
    status:
      description.length === 0
        ? "missing"
        : description.length >= META_DESCRIPTION_MIN && description.length <= META_DESCRIPTION_MAX
          ? "ok"
          : "warn",
    value: description.length,
  });
  if (keyword.length === 0) {
    // Asked for once; the placement items only make sense once there is a term to place.
    checks.push({ id: "searchTerms", status: "missing" });
  } else {
    checks.push({ id: "searchTerms", status: containsKeyword(text, keyword) ? "ok" : "warn" });
    checks.push({ id: "keywordInTitle", status: containsKeyword(title, keyword) ? "ok" : "warn" });
    checks.push({ id: "keywordInUrl", status: slugContainsKeyword(input.slug, keyword) ? "ok" : "warn" });
    checks.push({ id: "keywordEarly", status: containsKeyword(firstParagraph(input.contentHtml), keyword) ? "ok" : "warn" });
  }
  const headingCount = headings(input.contentHtml).length;
  checks.push({ id: "headings", status: headingCount > 0 ? "ok" : "warn", value: headingCount });
  const linkCount = linkCountOf(input.contentHtml);
  checks.push({ id: "links", status: linkCount > 0 ? "ok" : "warn", value: linkCount });
  checks.push({ id: "url", status: isDescriptiveSlug(input.slug) ? "ok" : "warn" });

  if (input.hasCover) {
    checks.push({ id: "coverAlt", status: input.coverAlt.trim().length > 0 ? "ok" : "missing" });
  }
  const alts = imageAlts(input.contentHtml);
  if (alts.length > 0) {
    const withAlt = alts.filter((a) => a.length > 0).length;
    checks.push({ id: "bodyImageAlts", status: withAlt === alts.length ? "ok" : withAlt > 0 ? "warn" : "missing", value: withAlt });
  }

  // Emitted by the page for every published post; listed so the author sees it is covered.
  checks.push({ id: "structuredData", status: "ok" });

  return checks;
}

/** The slug's segments hold the keyword — as a run, or every word of it somewhere in it. */
export function slugContainsKeyword(slug: string, keyword: string): boolean {
  const asSlug = keyword.trim().length === 0 ? "" : slugFromTitle(keyword);
  if (asSlug.length === 0) return false;
  if (slug.includes(asSlug)) return true;
  const segments = new Set(slug.split("-"));
  return asSlug.split("-").every((part) => segments.has(part));
}

/** Links in the body with an href — internal or external; a bare anchor is not one. */
export function linkCountOf(html: string): number {
  return [...html.matchAll(/<a\b[^>]*\bhref\s*=\s*("[^"]+"|'[^']+')/gi)].length;
}

/** Words joined by hyphens, more than one of them, and not the generator's fallback. */
export function isDescriptiveSlug(slug: string): boolean {
  const parts = slug.split("-").filter((p) => p.length > 0);
  return parts.length >= 2 && parts.some((p) => /\p{L}/u.test(p)) && slug !== "post" && slug.length <= SLUG_MAX_LENGTH;
}

export interface SeoScore {
  ok: number;
  total: number;
  /** good ≥ 80 %, fair ≥ 50 %, else poor — the sidebar card's colour. */
  band: "good" | "fair" | "poor";
}

export function seoScore(checks: SeoCheck[]): SeoScore {
  const ok = checks.filter((c) => c.status === "ok").length;
  const total = checks.length;
  const ratio = total === 0 ? 0 : ok / total;
  return { ok, total, band: ratio >= 0.8 ? "good" : ratio >= 0.5 ? "fair" : "poor" };
}
