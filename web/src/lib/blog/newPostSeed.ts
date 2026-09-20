import type { BlogLanguage } from "@/types/api";

/**
 * Where a new post starts (2026-09-20): the language, and the post it is the translation of when
 * the admin table's "add translation" opened the editor — the link then goes out with the first
 * save, so the pair is linked before the author has typed a title.
 */
export interface NewPostSeed {
  language: BlogLanguage;
  translationOfPostId: string | null;
}

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/** The address the table links to for a missing side. */
export function newTranslationHref(language: BlogLanguage, translationOfPostId: string): string {
  return `/admin/blog/new?lang=${language}&translationOf=${translationOfPostId}`;
}

/**
 * What `/admin/blog/new?lang=en&translationOf=<id>` asks for. Anything that is not one of the
 * two languages or not an id is ignored rather than sent on: the API would refuse it, but a bad
 * link should open a plain new post, not an error.
 */
export function newPostSeedFrom(params: URLSearchParams): NewPostSeed | undefined {
  const lang = params.get("lang");
  const translationOf = params.get("translationOf");
  if (lang !== "tr" && lang !== "en") return undefined;
  return {
    language: lang,
    translationOfPostId: translationOf && GUID.test(translationOf) ? translationOf : null,
  };
}
