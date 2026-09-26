/**
 * The generated cover of a blog post (DECISIONS.md 2026-09-27): what it says, how big, and which
 * icon. Pure, so the list card, the editor's preview and the share-image route agree on every
 * rule — and so the rules are testable without rendering anything.
 *
 * The cover is drawn on a 1200×630 grid (the share image's own size); every size below is in that
 * grid's pixels, and the HTML card scales them to its width.
 */

/** The API's cap on the cover line (`BlogCoverCard.MaxHookLength`); the editor's counter shows it. */
export const COVER_HOOK_MAX = 60;

/** What a post without a chosen icon shows — a plain "article" mark that fits any post. */
export const DEFAULT_COVER_ICON = "newspaper";

/** Past this the text no longer fits three lines at the smallest size; the rest becomes "…". */
export const COVER_TEXT_MAX = 110;

/** One icon as Lucide ships it (`lucide-static/icon-nodes.json`): its SVG children, tag and attributes. */
export type CoverIconNode = [tag: string, attributes: Record<string, string>][];

/** The shape of every name in the icon set — the same rule the API stores by (`BlogCoverCard.IsIconName`). */
const ICON_NAME = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

export function isCoverIconName(value: string | null | undefined): value is string {
  return typeof value === "string" && value.length > 0 && value.length <= 64 && ICON_NAME.test(value);
}

/**
 * The line the cover prints: the post's own cover line, else its title — whitespace folded, and
 * cut on a word boundary past {@link COVER_TEXT_MAX} so a long title cannot overflow the card.
 */
export function coverText(title: string, hook: string | null | undefined): string {
  const source = (hook ?? "").trim() || title;
  const folded = source.replace(/\s+/g, " ").trim();
  if (folded.length <= COVER_TEXT_MAX) return folded;
  const cut = folded.slice(0, COVER_TEXT_MAX);
  const lastSpace = cut.lastIndexOf(" ");
  return `${lastSpace > COVER_TEXT_MAX * 0.6 ? cut.slice(0, lastSpace) : cut}…`;
}

/** The text's size on the 1200-wide grid: a short line is set large, a title-length one smaller, so
 *  every cover reads at list-card size and still fits in three lines. */
export function coverTextSize(text: string): number {
  const length = text.length;
  if (length <= 24) return 88;
  if (length <= 40) return 76;
  if (length <= 60) return 64;
  if (length <= 85) return 54;
  return 46;
}

/** The icon to draw: the chosen one when the set has it, else the default. A name the set does
 *  not know (renamed in a later Lucide, or never there) draws the default rather than nothing. */
export function resolveCoverIcon(nodes: Record<string, CoverIconNode>, name: string | null | undefined): CoverIconNode | null {
  if (isCoverIconName(name) && Object.hasOwn(nodes, name)) return nodes[name];
  return nodes[DEFAULT_COVER_ICON] ?? null;
}
