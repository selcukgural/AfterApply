/**
 * Which picture a shared link shows (DECISIONS.md 2026-09-21): the post's cover when it is
 * big and wide enough for a share card, else the site's generated card. The numbers are the
 * card's own — 1200×630 is what Open Graph consumers render, 1.91:1 its ratio; a cover that is
 * smaller is upscaled and blurred, one that is square or tall is cropped to a band. The rule is
 * one function so the page, the editor's note and the preview cannot disagree.
 */
export const SHARE_IMAGE_MIN_WIDTH = 1200;
export const SHARE_IMAGE_MIN_HEIGHT = 630;
export const SHARE_IMAGE_MIN_RATIO = 1.6;
export const SHARE_IMAGE_MAX_RATIO = 2.1;

export interface ImageSize {
  width: number | null;
  height: number | null;
}

export type ShareImageVerdict =
  /** Big and wide enough: the cover is the share image. */
  | "cover"
  /** Smaller than the card. */
  | "tooSmall"
  /** Square or tall; the card would crop it. */
  | "wrongRatio"
  /** The upload could not read the bytes' size, so the safe choice is the generated card. */
  | "unknownSize";

export function shareImageVerdict(size: ImageSize | null | undefined): ShareImageVerdict | null {
  if (!size) return null;
  const { width, height } = size;
  if (!width || !height) return "unknownSize";
  if (width < SHARE_IMAGE_MIN_WIDTH || height < SHARE_IMAGE_MIN_HEIGHT) return "tooSmall";
  const ratio = width / height;
  if (ratio < SHARE_IMAGE_MIN_RATIO || ratio > SHARE_IMAGE_MAX_RATIO) return "wrongRatio";
  return "cover";
}

/** True when a post with this cover shares the cover rather than the generated card. */
export function coverIsShareImage(size: ImageSize | null | undefined): boolean {
  return shareImageVerdict(size) === "cover";
}
