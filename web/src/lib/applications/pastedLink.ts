/**
 * Whether text pasted into the applications search reads as a posting link rather than words to
 * search for. The server decides what the link matches (JobUrlSearchKey); this only decides
 * whether a paste is worth jumping straight to the one application it finds.
 */
export function looksLikeJobLink(text: string): boolean {
  const value = text.trim();
  if (value === "" || /\s/.test(value)) return false;
  if (/^https?:\/\/[^/]+\.[^/]+/i.test(value)) return true;
  return /^(www\.)?[a-z0-9-]+(\.[a-z0-9-]+)+\/\S+/i.test(value);
}

/**
 * The search term that asks "is this posting already in my list?" for what the user typed into the
 * new-application form's posting field, or null when it does not read as a link yet — half a
 * typed address must not warn about some other posting.
 */
export function savedPostingSearch(jobUrl: string): string | null {
  const value = jobUrl.trim();
  return looksLikeJobLink(value) ? value : null;
}
