/**
 * The share image every public page carries.
 *
 * Until 2026-09-14 only the landing page had an `og:image`: `app/[locale]/opengraph-image.tsx`
 * applies to that one route, and `buildMetadata` — which every other public page goes through —
 * set no image at all. So a guide article, a company page or the CV-scan page shared on LinkedIn
 * (the one channel that actually sends visitors) rendered as a bare grey card. The fix is a route
 * that draws the page's own title onto the brand card, and a helper that gives `buildMetadata`
 * that route's URL.
 *
 * The title travels in the query string, so the two functions below are the whole contract between
 * the metadata side and the route: what one puts in, the other takes out — clamped, so a crafted URL
 * cannot make the renderer lay out a novel.
 */

export const OG_IMAGE_WIDTH = 1200;
export const OG_IMAGE_HEIGHT = 630;

/** Long enough for any real page title on two lines at the card's font size; short enough that
 *  nothing a stranger types into the URL can overflow the card. */
export const OG_TITLE_MAX_LENGTH = 110;
export const OG_KICKER_MAX_LENGTH = 48;

/**
 * Collapses whitespace, strips control characters and clamps the length. Pure, so the route's
 * behaviour on a hostile query string is testable without rendering an image.
 */
export function sanitizeOgText(value: string | null | undefined, maxLength: number): string {
  if (!value) return "";
  const cleaned = value
    .replace(/[\u0000-\u001f\u007f-\u009f]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
  if (cleaned.length <= maxLength) return cleaned;
  // Cut on a word boundary where there is one, so the card never ends mid-word.
  const cut = cleaned.slice(0, maxLength);
  const lastSpace = cut.lastIndexOf(" ");
  return `${lastSpace > maxLength * 0.6 ? cut.slice(0, lastSpace) : cut}…`;
}

/**
 * The path `buildMetadata` puts in `og:image` for one page. Relative — it resolves against
 * `metadataBase` for the HTML and is absolutised by the caller for the sitemap-less places that
 * need it. `kicker` is the small line above the title ("Rehber", "Şirket değerlendirmeleri").
 */
export function ogImagePath(locale: string, title: string, kicker?: string): string {
  const params = new URLSearchParams();
  params.set("t", sanitizeOgText(title, OG_TITLE_MAX_LENGTH));
  const cleanKicker = sanitizeOgText(kicker, OG_KICKER_MAX_LENGTH);
  if (cleanKicker) params.set("k", cleanKicker);
  return `/${locale}/og?${params.toString()}`;
}
