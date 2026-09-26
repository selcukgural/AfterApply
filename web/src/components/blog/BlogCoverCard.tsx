import { createElement, type CSSProperties } from "react";
import { OG_IMAGE_HEIGHT, OG_IMAGE_WIDTH } from "@/lib/seo/ogImage";
import { coverTextSize, type CoverIconNode } from "@/lib/blog/coverCard";

/**
 * A blog post's generated cover (DECISIONS.md 2026-09-27, canvas "A + K2 + simge"): the light-blue
 * brand card with the post's line, one icon and the site's name. One component for every place
 * the cover appears, so they cannot drift apart:
 *
 * - `html` — the list card and the editor's preview. Fluid: it fills its box at 1200:630 and sizes
 *   everything in container-query units, so the card looks the same at 340 px and at 500 px.
 * - `og` — the share image, rendered by `next/og` at exactly 1200×630. Satori reads a subset of
 *   CSS (flexbox, every element with more than one child `display: flex`, no container units), so
 *   the same numbers are written as pixels there.
 *
 * Every size below is a pixel on the 1200-wide grid; `u()` turns it into what the mode needs.
 * The colours are fixed, dark mode included: the cover is a picture, the way an uploaded one is.
 */
const COLOURS = {
  background: "#eef3fd",
  border: "#d6e1f7",
  text: "#0f1f45",
  secondary: "#3b5bab",
  icon: "#2a5fd6",
} as const;

export interface BlogCoverCardProps {
  /** Already chosen by `coverText` — the cover line, or the title. */
  text: string;
  /** The icon's drawing; null draws the card without one. */
  icon: CoverIconNode | null;
  /** The small label over the text ("Blog"). */
  eyebrow: string;
  mode?: "html" | "og";
}

export function BlogCoverCard({ text, icon, eyebrow, mode = "html" }: BlogCoverCardProps) {
  const og = mode === "og";
  const u = (px: number): number | string => (og ? px : `${(px / 12).toFixed(3)}cqw`);
  // A length inside a shorthand string needs its unit written out in either mode.
  const len = (px: number): string => (og ? `${px}px` : `${(px / 12).toFixed(3)}cqw`);
  const fontSize = coverTextSize(text);

  const card: CSSProperties = {
    position: "relative",
    display: "flex",
    flexDirection: "column",
    justifyContent: "space-between",
    boxSizing: "border-box",
    padding: `${len(56)} ${len(64)}`,
    background: COLOURS.background,
    color: COLOURS.text,
    overflow: "hidden",
    ...(og ? { width: OG_IMAGE_WIDTH, height: OG_IMAGE_HEIGHT, fontFamily: "Geist" } : { width: "100%", height: "100%" }),
  };

  const content = (
    <div style={card}>
      {icon && (
        <svg
          width={og ? 150 : undefined}
          height={og ? 150 : undefined}
          viewBox="0 0 24 24"
          fill="none"
          stroke={COLOURS.icon}
          strokeWidth={1.6}
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
          style={{ position: "absolute", right: u(60), top: u(52), width: u(150), height: u(150) }}
        >
          {icon.map(([tag, attributes], index) => createElement(tag, { key: index, ...attributes }))}
        </svg>
      )}
      <div style={{ display: "flex", fontSize: u(28), fontWeight: 600, letterSpacing: u(2.4), textTransform: "uppercase", color: COLOURS.secondary }}>
        {eyebrow}
      </div>
      <div
        style={{
          display: og ? "flex" : "-webkit-box",
          fontSize: u(fontSize),
          fontWeight: 600,
          lineHeight: 1.12,
          letterSpacing: u(-fontSize * 0.012),
          maxWidth: u(900),
          ...(og ? {} : { WebkitLineClamp: 3, WebkitBoxOrient: "vertical" as const, overflow: "hidden", overflowWrap: "anywhere" as const }),
        }}
      >
        {text}
      </div>
      <div style={{ display: "flex", fontSize: u(30), fontWeight: 600, color: COLOURS.secondary }}>e-kariyerim</div>
    </div>
  );

  if (og) return content;

  // The box the card's units are measured against. The border and the corners belong to the page,
  // not to the picture (the share image is a plain rectangle).
  return (
    <div
      style={{ containerType: "inline-size", aspectRatio: `${OG_IMAGE_WIDTH} / ${OG_IMAGE_HEIGHT}`, borderColor: COLOURS.border }}
      className="w-full overflow-hidden rounded-xl border"
    >
      {content}
    </div>
  );
}
