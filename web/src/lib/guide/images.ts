/**
 * The pictures the guide articles use, by their path in `public/`, with their pixel size.
 *
 * Markdown's `![alt](src)` carries no dimensions, and an image without them makes the prose below
 * it jump as it loads (layout shift, which Google measures). The article body stays plain markdown;
 * the size comes from here. A path that is not listed is a build error in mdx-components.tsx rather
 * than an unsized image in production, and articles.test.ts checks each size against the file.
 */
export const GUIDE_IMAGES: Record<string, { width: number; height: number }> = {
  // The shared flow card's link format at 2×, rendered from the local example card
  // 86-47-0-21-0-0-18-2-4-9-3-0-11-202606-202609 — the numbers belong to no real person.
  "/guide/basvuru-akis-karti-ornegi.png": { width: 2400, height: 1260 },
  "/guide/job-application-flow-card-example.png": { width: 2400, height: 1260 },
};

export function guideImageSize(src: string): { width: number; height: number } {
  const size = GUIDE_IMAGES[src];
  if (!size) throw new Error(`Guide image "${src}" is not listed in GUIDE_IMAGES`);
  return size;
}
