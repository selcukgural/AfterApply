/**
 * The four image sizes the flow card is drawn at (2026-09-23 canvas, "A · Platformlar"). A shared
 * *link* always shows `link` — LinkedIn, X, Facebook and WhatsApp all read the page's 1200×630
 * og:image — while a downloaded *image* is sized for where it will be posted:
 *
 *  - `x`         1600×900 (16:9)  X posts
 *  - `portrait`  1080×1350 (4:5)  LinkedIn, Facebook and Instagram feed posts
 *  - `story`     1080×1920 (9:16) Instagram / Facebook stories, WhatsApp status — the top 260 px
 *                and bottom 290 px are left to the apps' own controls
 *
 * Each size lays the diagram out again at its own proportions (lib/flowCard/layout.ts) rather
 * than scaling one picture, so the thin branches stay readable on a tall card.
 */

export type FlowFormat = "link" | "x" | "portrait" | "story";

export const FLOW_FORMATS: readonly FlowFormat[] = ["link", "x", "portrait", "story"];

export interface FlowFormatSpec {
  width: number;
  height: number;
  padding: [number, number, number, number];
  wordmark: number;
  date: number;
  headlineGap: number;
  headline: number;
  sublineGap: number;
  subline: number;
  diagramGap: number;
  diagramWidth: number;
  diagramHeight: number;
  diagramScale: number;
  footerStacked: boolean;
  footerGap: number;
  cta: number;
  footnote: number;
}

export const FLOW_FORMAT_SPECS: Record<FlowFormat, FlowFormatSpec> = {
  link: {
    width: 1200, height: 630, padding: [36, 56, 32, 56], wordmark: 20, date: 14,
    headlineGap: 14, headline: 42, sublineGap: 8, subline: 17,
    diagramGap: 18, diagramWidth: 1088, diagramHeight: 350, diagramScale: 1,
    footerStacked: false, footerGap: 16, cta: 16, footnote: 13,
  },
  x: {
    width: 1600, height: 900, padding: [52, 72, 44, 72], wordmark: 26, date: 18,
    headlineGap: 18, headline: 58, sublineGap: 10, subline: 22,
    diagramGap: 26, diagramWidth: 1456, diagramHeight: 520, diagramScale: 1.35,
    footerStacked: false, footerGap: 20, cta: 21, footnote: 17,
  },
  portrait: {
    width: 1080, height: 1350, padding: [64, 64, 56, 64], wordmark: 26, date: 18,
    headlineGap: 24, headline: 68, sublineGap: 14, subline: 24,
    diagramGap: 36, diagramWidth: 952, diagramHeight: 760, diagramScale: 1.3,
    footerStacked: false, footerGap: 20, cta: 21, footnote: 16,
  },
  story: {
    width: 1080, height: 1920, padding: [260, 64, 290, 64], wordmark: 30, date: 20,
    headlineGap: 28, headline: 80, sublineGap: 16, subline: 28,
    diagramGap: 44, diagramWidth: 952, diagramHeight: 840, diagramScale: 1.35,
    footerStacked: true, footerGap: 22, cta: 26, footnote: 19,
  },
};

/** The same card at `density` times the pixels — every measure multiplied, the layout unchanged. */
export function scaleFlowFormatSpec(spec: FlowFormatSpec, density: number): FlowFormatSpec {
  if (density === 1) return spec;
  const scaled = Object.fromEntries(
    Object.entries(spec).map(([key, value]) => [
      key,
      typeof value === "number" ? value * density : Array.isArray(value) ? value.map((part) => part * density) : value,
    ]),
  );
  return scaled as unknown as FlowFormatSpec;
}

export function parseFlowFormat(value: string | null | undefined): FlowFormat | null {
  return FLOW_FORMATS.includes(value as FlowFormat) ? (value as FlowFormat) : null;
}
