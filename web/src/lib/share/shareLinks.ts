/**
 * The share targets behind every "share" button on the public site (growth audit 2026-09-14,
 * findings 03 and 14): a CV score, a benchmark result, a company page. Kept pure — a URL and a
 * sentence in, a set of links out — so the exact URLs can be tested without a DOM.
 *
 * Nothing here loads a third-party script or widget: each target is a plain link to that
 * network's own share page, which the CSP and the cookie policy already allow (a navigation, not
 * a script). LinkedIn's share page takes only the URL and reads the title from the page's own
 * Open Graph card, which every public page carries since 2026-09-14.
 */

export type ShareTarget = "linkedin" | "whatsapp" | "x" | "facebook";

export interface ShareContent {
  /** What the person is passing on, without the link — the link is appended per target. */
  text: string;
  /** The absolute URL of the page the share points at. */
  url: string;
}

/** The row the public share control shows. Facebook is reachable through `shareHref` for the flow
 *  card's share dialog (2026-09-23), which picks one network at a time, but stays off this row. */
export const SHARE_TARGETS: readonly ShareTarget[] = ["linkedin", "whatsapp", "x"];

export function shareHref(target: ShareTarget, content: ShareContent): string {
  const url = encodeURIComponent(content.url);
  switch (target) {
    case "linkedin":
      return `https://www.linkedin.com/sharing/share-offsite/?url=${url}`;
    case "whatsapp":
      return `https://wa.me/?text=${encodeURIComponent(`${content.text} ${content.url}`)}`;
    case "x":
      return `https://twitter.com/intent/tweet?text=${encodeURIComponent(content.text)}&url=${url}`;
    case "facebook":
      return `https://www.facebook.com/sharer/sharer.php?u=${url}`;
  }
}

/**
 * What "copy link" puts on the clipboard: the link and nothing else (2026-09-19). The button
 * says "link", and a pasted "Title https://…" read as a mistake next to it; the sentence still
 * rides along on WhatsApp, X and the native share sheet, where it is the message.
 */
export function shareClipboardText(content: ShareContent): string {
  return content.url;
}
