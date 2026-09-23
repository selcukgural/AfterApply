import type { FlowFormat } from "./formats";
import { FLOW_FORMAT_SPECS } from "./formats";

/**
 * What the flow card's share dialog offers per network (2026-09-23 canvas): the person picks where
 * they are posting first, and only the outputs that fit there are shown. An image output is a PNG
 * at that network's size; a link output shares the card's page, whose preview is always the
 * 1200×630 `link` image — the networks draw that preview themselves.
 */

export type SharePlatform = "linkedin" | "x" | "facebook" | "instagram" | "whatsapp";

export const SHARE_PLATFORMS: readonly SharePlatform[] = ["linkedin", "x", "facebook", "instagram", "whatsapp"];

export const PLATFORM_NAME: Record<SharePlatform, string> = {
  linkedin: "LinkedIn",
  x: "X",
  facebook: "Facebook",
  instagram: "Instagram",
  whatsapp: "WhatsApp",
};

export type ShareOutput =
  | { kind: "image"; label: "post" | "story" | "status"; format: FlowFormat; note: string }
  | { kind: "link"; label: "link"; format: "link"; note: "link" };

const post = (format: FlowFormat, note: string): ShareOutput => ({ kind: "image", label: "post", format, note });
const story: ShareOutput = { kind: "image", label: "story", format: "story", note: "story" };
const link: ShareOutput = { kind: "link", label: "link", format: "link", note: "link" };

export const SHARE_OUTPUTS: Record<SharePlatform, readonly ShareOutput[]> = {
  linkedin: [post("portrait", "linkedinPost"), link],
  x: [post("x", "xPost"), link],
  facebook: [post("portrait", "facebookPost"), story, link],
  // Instagram has no clickable links in posts, so no link output.
  instagram: [post("portrait", "instagramPost"), story],
  whatsapp: [link, { ...story, label: "status" }],
};

/** "1080×1350" — the network's size for an output; `density` 2 gives the downloaded file's pixels. */
export function outputSize(output: ShareOutput, density = 1): string {
  const spec = FLOW_FORMAT_SPECS[output.format];
  return `${spec.width * density}×${spec.height * density}`;
}
