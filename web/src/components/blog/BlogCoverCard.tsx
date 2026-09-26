import { createElement, type CSSProperties } from "react";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";
import { coverTextSize, coverTone, type CoverIconNode } from "@/lib/blog/coverCard";

/**
 * A blog post's generated cover (DECISIONS.md 2026-09-27, canvas "D3 · Sıcak kâğıt, güneş"): warm
 * cream paper, one large coloured circle in the corner with the post's icon on it, a thin ring
 * above it, the post's line in navy and the site's name. The colour comes from the icon
 * (`coverTone`), so a post and its translation match and different topics look different. One
 * component for every place the cover appears, so they cannot drift apart:
 *
 * - `html` — the list card and the editor's preview. Fluid: it fills its box at 1200:630 and sizes
 *   everything in container-query units, so the card looks the same at 340 px and at 500 px.
 * - `og` — the share image, rendered by `next/og` at exactly 1200×630. Satori reads a subset of
 *   CSS (flexbox, every element with more than one child `display: flex`, no container units), so
 *   the same numbers are written as pixels there.
 *
 * Every size below is a pixel on the 1200-wide grid; `u()` turns it into what the mode needs.
 * The colours are fixed, dark mode included: the cover is a picture, the way an uploaded one is.
 * The uploaded covers of the first posts were drawn from the same numbers (2026-09-27).
 */
const PAPER = "#fff6ec";
const PAPER_BORDER = "#f1e2cf";
const TEXT = "#1f2a44";

export interface BlogCoverCardProps {
  /** Already chosen by `coverText` — the cover line, or the title. */
  text: string;
  /** The icon's drawing; null draws the card without one. */
  icon: CoverIconNode | null;
  /** The icon's name as stored (null: the default) — it picks the colour. */
  iconName: string | null;
  /** The small label over the text ("Blog"). */
  eyebrow: string;
  mode?: "html" | "og";
}

export function BlogCoverCard({ text, icon, iconName, eyebrow, mode = "html" }: BlogCoverCardProps) {
  const og = mode === "og";
  const u = (px: number): number | string => (og ? px : `${(px / 12).toFixed(3)}cqw`);
  // A length inside a shorthand string needs its unit written out in either mode.
  const len = (px: number): string => (og ? `${px}px` : `${(px / 12).toFixed(3)}cqw`);
  const fontSize = coverTextSize(text);
  const tone = coverTone(iconName);

  const card: CSSProperties = {
    position: "relative",
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
    boxSizing: "border-box",
    padding: `${len(60)} ${len(70)}`,
    background: PAPER,
    color: TEXT,
    overflow: "hidden",
    ...(og ? { width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT, fontFamily: "Geist" } : { width: "100%", height: "100%" }),
  };

  const content = (
    <div style={card}>
      <div style={{ position: "absolute", right: u(-230), bottom: u(-300), width: u(720), height: u(720), borderRadius: 9999, background: tone.sun, display: "flex" }} />
      <div
        style={{
          position: "absolute",
          right: u(330),
          top: u(60),
          width: u(110),
          height: u(110),
          boxSizing: "border-box",
          borderRadius: 9999,
          border: `${len(7)} solid ${tone.sun}`,
          opacity: 0.5,
          display: "flex",
        }}
      />
      {icon && (
        <svg
          width={og ? 190 : undefined}
          height={og ? 190 : undefined}
          viewBox="0 0 24 24"
          fill="none"
          stroke="#ffffff"
          strokeWidth={1.8}
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          style={{ position: "absolute", right: u(120), bottom: u(100), width: u(190), height: u(190) }}
        >
          {icon.map(([tag, attributes], index) => createElement(tag, { key: index, ...attributes }))}
        </svg>
      )}
      <div style={{ position: "relative", display: "flex", fontSize: u(27), fontWeight: 600, letterSpacing: u(2.7), textTransform: "uppercase", color: tone.ink }}>
        {eyebrow}
      </div>
      <div
        style={{
          position: "relative",
          display: og ? "flex" : "-webkit-box",
          fontSize: u(fontSize),
          fontWeight: 700,
          lineHeight: 1.12,
          letterSpacing: u(-fontSize * 0.01),
          color: TEXT,
          maxWidth: u(620),
          ...(og ? {} : { WebkitLineClamp: 4, WebkitBoxOrient: "vertical" as const, overflow: "hidden", overflowWrap: "anywhere" as const }),
        }}
      >
        {text}
      </div>
      <div style={{ position: "relative", display: "flex", fontSize: u(30), fontWeight: 600, color: tone.ink }}>e-kariyerim</div>
    </div>
  );

  if (og) return content;

  // The box the card's units are measured against. The border and the corners belong to the page,
  // not to the picture (the share image is a plain rectangle).
  return (
    <div
      style={{ containerType: "inline-size", aspectRatio: `${OG_IMAGE_WIDTH} / ${OG_IMAGE_HEIGHT}`, borderColor: PAPER_BORDER }}
      className="w-full overflow-hidden rounded-xl border"
    >
      {content}
    </div>
  );
}
