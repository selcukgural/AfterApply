import type { SharePlatform } from "./sharePlan";

/**
 * How each network looks in the share dialog's platform row (2026-09-23 canvas, page "C · iki
 * temada", board "Önerilen"): a neutral circle with the brand's mark in its own colour, filled with
 * the brand colour once picked. The share button itself stays the site's primary button, so the
 * brand colour only ever marks the one circle that is chosen.
 *
 * Colours are each network's published brand colour (LinkedIn #0A66C2, Facebook #0866FF,
 * WhatsApp #25D366, Instagram's orange→magenta→purple gradient). X's mark is black on white and
 * white on black, so it follows the theme's ink instead of a fixed colour — the one mark that
 * would otherwise vanish in one of the two themes.
 */
export interface PlatformStyle {
  /** The mark on the neutral circle: a hex, `gradient` (Instagram) or `ink` (X: the text colour). */
  mark: string;
  /** Classes for the picked circle — the brand fill and the mark's colour on it. */
  picked: string;
}

export const BRAND_GRADIENT_STOPS = ["#F58529", "#DD2A7B", "#8134AF"] as const;

export const PLATFORM_STYLE: Record<SharePlatform, PlatformStyle> = {
  linkedin: { mark: "#0A66C2", picked: "bg-[#0A66C2] text-white" },
  x: { mark: "ink", picked: "bg-black text-white dark:bg-white dark:text-black" },
  facebook: { mark: "#0866FF", picked: "bg-[#0866FF] text-white" },
  instagram: { mark: "gradient", picked: "bg-linear-45 from-[#F58529] via-[#DD2A7B] to-[#8134AF] text-white" },
  whatsapp: { mark: "#25D366", picked: "bg-[#25D366] text-white" },
};
