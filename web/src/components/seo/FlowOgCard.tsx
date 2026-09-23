import type { ApplicationFlowCounts } from "@/types/api";
import { FLOW_FORMAT_SPECS, scaleFlowFormatSpec, type FlowFormat, type FlowFormatSpec } from "@/lib/flowCard/formats";
import { layoutFlow, TONE_COLORS, FLOW_TONE, type FlowNodeKey } from "@/lib/flowCard/layout";

export interface FlowOgCardProps {
  format: FlowFormat;
  counts: ApplicationFlowCounts;
  /** Base64 data URI of the brand mark, read once by the route. */
  logoSrc: string;
  headline: string;
  subline: string;
  dateRange: string;
  headers: [string, string, string];
  names: Record<FlowNodeKey, string>;
  cta: string;
  footnote: string;
  /** 1 for the og:image crawlers fetch; 2 for what a person looks at or downloads on a
   *  high-density screen. The card is laid out once and scaled, so both are the same picture. */
  density?: 1 | 2;
  /** The route registers Geist under that name for satori; a browser page has it under
   *  next/font's variable instead (the share dialog's draft passes `var(--font-geist-sans)`). */
  fontFamily?: string;
}

const INK = "#111827";
const MUTED = "#5b6377";
const RULE = "#e8ebf2";

/**
 * The shareable flow card (2026-09-23 canvas, variant A) as satori draws it: white ground, the brand
 * mark and wordmark, the headline, the Sankey diagram and a footer. Unlike the dark OgCard this one
 * carries the real logo — the route hands it in as a data URI read from `public/` on the Node
 * runtime.
 *
 * Satori's rules shape the markup: flexbox only, every element with more than one child
 * `display: flex`. The ribbons and bars are one inline `<svg>`; labels are absolutely positioned
 * divs over it, because satori does not lay out `<text>` inside an svg with the card's font.
 *
 * The same markup is plain React DOM, so the share dialog draws it in the browser as the draft
 * shown while the PNG renders (FlowCardPreview) — layout and text identical by construction.
 */
export function FlowOgCard(props: FlowOgCardProps) {
  // Every measure multiplied rather than a CSS scale: satori ignores transform-origin, and drawing
  // at the larger size keeps text and ribbons sharp instead of stretched.
  return <FlowOgCardBody {...props} spec={scaleFlowFormatSpec(FLOW_FORMAT_SPECS[props.format], props.density ?? 1)} />;
}

function FlowOgCardBody(props: FlowOgCardProps & { spec: FlowFormatSpec }) {
  const { spec } = props;
  const d = spec.width / FLOW_FORMAT_SPECS[props.format].width;
  const layout = layoutFlow(props.counts, spec.diagramWidth, spec.diagramHeight, spec.diagramScale);
  const s = layout.scale;
  const [pt, pr, pb, pl] = spec.padding;
  const logoSize = Math.round(spec.wordmark * 1.5);

  // A white halo keeps a label legible where it sits on a ribbon (the "interviewed" node's name,
  // the total's count) — the canvas used an SVG paint-order stroke for the same thing.
  const halo = `0 0 ${3 * d}px #ffffff, 0 0 ${3 * d}px #ffffff, 0 0 ${3 * d}px #ffffff`;
  const labelX = (x: number) => x + layout.nodes[0].width + 12 * s;

  return (
    <div
      style={{
        width: spec.width,
        height: spec.height,
        display: "flex",
        flexDirection: "column",
        padding: `${pt}px ${pr}px ${pb}px ${pl}px`,
        background: "#ffffff",
        color: INK,
        fontFamily: props.fontFamily ?? "Geist",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div style={{ display: "flex", alignItems: "center", gap: Math.round(spec.wordmark * 0.5) }}>
          {/* eslint-disable-next-line @next/next/no-img-element -- satori renders <img>, not next/image */}
          <img src={props.logoSrc} width={logoSize} height={logoSize} alt="" />
          <div style={{ display: "flex", fontSize: spec.wordmark, fontWeight: 600, letterSpacing: -0.3 * d }}>e-kariyerim</div>
        </div>
        <div style={{ display: "flex", fontSize: spec.date, color: MUTED }}>{props.dateRange}</div>
      </div>

      <div
        style={{
          display: "flex",
          marginTop: spec.headlineGap,
          fontSize: spec.headline,
          fontWeight: 700,
          lineHeight: 1.1,
          letterSpacing: -spec.headline * 0.02,
        }}
      >
        {props.headline}
      </div>
      <div style={{ display: "flex", marginTop: spec.sublineGap, fontSize: spec.subline, color: MUTED }}>{props.subline}</div>

      <div
        style={{
          display: "flex",
          position: "relative",
          marginTop: spec.diagramGap,
          width: spec.diagramWidth,
          height: spec.diagramHeight,
        }}
      >
        <svg width={spec.diagramWidth} height={spec.diagramHeight} viewBox={`0 0 ${spec.diagramWidth} ${spec.diagramHeight}`}>
          {layout.bands.map((band) => (
            <path key={`${band.from}-${band.to}`} d={band.d} fill={TONE_COLORS[band.tone].band} />
          ))}
          {layout.nodes.map((node) => (
            <rect
              key={node.key}
              x={node.x}
              y={node.y}
              width={node.width}
              height={node.height}
              rx={3 * s}
              fill={TONE_COLORS[FLOW_TONE[node.key]].node}
            />
          ))}
        </svg>

        {layout.columnX.map((x, column) =>
          column === 2 && !layout.hasSecondColumn ? null : (
            <div
              key={`header-${column}`}
              style={{
                position: "absolute",
                left: x,
                top: 0,
                display: "flex",
                fontSize: 12 * s,
                fontWeight: 600,
                letterSpacing: 0.8 * s,
                color: MUTED,
              }}
            >
              {props.headers[column]}
            </div>
          ),
        )}

        {layout.nodes.map((node) => {
          const ink = node.key === "total" ? MUTED : TONE_COLORS[FLOW_TONE[node.key]].ink;
          const emphasis = node.key === "unanswered" || node.key === "silentAfterInterview" || node.key === "offer";
          const countColor = node.key === "unanswered" ? TONE_COLORS.red.ink : INK;
          const big = node.key === "total" || node.key === "unanswered" ? 30 : 22;

          if (node.twoLine) {
            const blockHeight = (15 + big) * 1.15 * s;
            return (
              <div
                key={`label-${node.key}`}
                style={{
                  position: "absolute",
                  left: labelX(node.x),
                  top: node.slotMid - blockHeight / 2,
                  display: "flex",
                  flexDirection: "column",
                  textShadow: halo,
                }}
              >
                <div style={{ display: "flex", fontSize: 15 * s, color: ink, fontWeight: emphasis ? 600 : 400 }}>
                  {props.names[node.key]}
                </div>
                <div style={{ display: "flex", fontSize: big * s, fontWeight: 700, color: countColor, lineHeight: 1.1 }}>
                  {String(node.count)}
                </div>
              </div>
            );
          }

          return (
            <div
              key={`label-${node.key}`}
              style={{
                position: "absolute",
                left: labelX(node.x),
                top: node.slotMid - 9 * s,
                display: "flex",
                fontSize: 15 * s,
                lineHeight: 1.2,
                color: ink,
                fontWeight: emphasis ? 700 : 400,
                textShadow: halo,
              }}
            >
              {`${props.names[node.key]} · ${node.count}`}
            </div>
          );
        })}
      </div>

      <div
        style={{
          display: "flex",
          flexDirection: spec.footerStacked ? "column" : "row",
          justifyContent: "space-between",
          alignItems: spec.footerStacked ? "flex-start" : "center",
          gap: 6 * d,
          marginTop: "auto",
          paddingTop: spec.footerGap,
          borderTop: `${d}px solid ${RULE}`,
        }}
      >
        <div style={{ display: "flex", fontSize: spec.cta, fontWeight: 600 }}>{props.cta}</div>
        <div style={{ display: "flex", fontSize: spec.footnote, color: MUTED }}>{props.footnote}</div>
      </div>
    </div>
  );
}
