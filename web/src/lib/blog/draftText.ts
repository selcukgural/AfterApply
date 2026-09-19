/**
 * Whether a draft has anything written in it — the editor's copy of the server's
 * `BlogDraftText.HasAny` (2026-09-19). A new post is created on the first autosave that carries
 * at least one character in the title, the summary or the body; until then the editor keeps
 * everything in memory and sends nothing, so a "new post" opened and closed again leaves no row.
 * The body is HTML: tags are dropped and entities decoded before looking, so an empty paragraph
 * or one holding only `&nbsp;` is not text.
 */
export interface DraftText {
  title: string;
  excerpt: string | null;
  contentHtml: string;
}

export function hasDraftText({ title, excerpt, contentHtml }: DraftText): boolean {
  return title.trim() !== "" || (excerpt ?? "").trim() !== "" || textOf(contentHtml).trim() !== "";
}

/** The body's text with its markup removed — only good enough to tell "something" from
 *  "nothing"; not a renderer. */
export function textOf(contentHtml: string): string {
  return decodeEntities(contentHtml.replace(/<[^>]*>/g, " "));
}

// The entities an editor body can hold that read as blank once decoded; anything else decodes
// to a visible character and counts as text anyway.
function decodeEntities(text: string): string {
  return text.replace(/&(nbsp|#160|#xa0|#xA0|ensp|emsp|thinsp);/g, " ").replace(/&(amp|lt|gt|quot|#39);/g, "x");
}
