import type { ApplicationFlowCounts } from "@/types/api";
import { FIRST_COLUMN, SECOND_COLUMN } from "./card";

/**
 * Geometry of the flow card's Sankey diagram — pure, so the share image (satori, route
 * `/[locale]/og`) draws exactly what the tests check.
 *
 * Three columns: all applications → the first outcome → what happened after an interview. Band
 * widths are proportional to counts (one scale, `unit`, across the whole diagram); a node too small
 * to hold its label still gets a label-sized slot, so a single offer among eighty applications is
 * readable instead of overlapping its neighbour. Designed on the 2026-09-23 canvas (variant A).
 */

export type FlowNodeKey = "total" | (typeof FIRST_COLUMN)[number] | (typeof SECOND_COLUMN)[number];

export type FlowTone = "blue" | "red" | "gray" | "green" | "pale";

export const FLOW_TONE: Record<FlowNodeKey, FlowTone> = {
  total: "blue",
  unanswered: "red",
  awaitingReply: "pale",
  rejectedBeforeInterview: "gray",
  inScreening: "pale",
  withdrawnBeforeInterview: "gray",
  interviewed: "blue",
  offer: "green",
  interviewInProgress: "blue",
  rejectedAfterInterview: "gray",
  silentAfterInterview: "red",
  withdrawnAfterInterview: "gray",
};

/** Solid node fill, translucent band fill and the label ink per tone — the site's own tokens. */
export const TONE_COLORS: Record<FlowTone, { node: string; band: string; ink: string }> = {
  blue: { node: "#2a5fd6", band: "rgba(42,95,214,0.22)", ink: "#2551b8" },
  red: { node: "#e34948", band: "rgba(227,73,72,0.22)", ink: "#b32d2c" },
  gray: { node: "#8b93a7", band: "rgba(139,147,167,0.28)", ink: "#5b6377" },
  green: { node: "#1baf7a", band: "rgba(27,175,122,0.4)", ink: "#0f7a55" },
  pale: { node: "#9db8ef", band: "rgba(157,184,239,0.3)", ink: "#5b6377" },
};

export interface FlowNode {
  key: FlowNodeKey;
  column: 0 | 1 | 2;
  count: number;
  x: number;
  y: number;
  width: number;
  height: number;
  /** The label-sized slot the node sits centred in; its label is centred on `slotMid`. */
  slotMid: number;
  /** Two lines (name over a large count) when the slot has room, else "Name · N" on one. */
  twoLine: boolean;
}

export interface FlowBand {
  from: FlowNodeKey;
  to: FlowNodeKey;
  /** SVG path, a closed ribbon between two cubic curves. */
  d: string;
  tone: FlowTone;
}

export interface FlowLayout {
  width: number;
  height: number;
  scale: number;
  /** Where each column starts, for its header. */
  columnX: [number, number, number];
  /** The top of the diagram under the column headers. */
  top: number;
  hasSecondColumn: boolean;
  nodes: FlowNode[];
  bands: FlowBand[];
}

const round = (value: number) => Math.round(value * 10) / 10;

function ribbon(xa: number, a0: number, a1: number, xb: number, b0: number, b1: number): string {
  const mid = round((xa + xb) / 2);
  return (
    `M${round(xa)},${round(a0)} C${mid},${round(a0)} ${mid},${round(b0)} ${round(xb)},${round(b0)} ` +
    `L${round(xb)},${round(b1)} C${mid},${round(b1)} ${mid},${round(a1)} ${round(xa)},${round(a1)} Z`
  );
}

/**
 * The largest unit (px per application) at which the slots — each max(count × unit, minSlot) —
 * and the gaps between them fit in `available`. Slots that hit the floor are taken out and the
 * rest re-solved until nothing changes.
 */
function solveUnit(values: number[], available: number, minSlot: number, gap: number): number {
  const space = available - gap * Math.max(0, values.length - 1);
  let floored = new Set<number>();
  for (let round = 0; round <= values.length; round++) {
    const free = values.reduce((sum, value, index) => (floored.has(index) ? sum : sum + value), 0);
    const room = space - floored.size * minSlot;
    const unit = free > 0 && room > 0 ? room / free : 0;
    const next = new Set(values.flatMap((value, index) => (value * unit < minSlot ? [index] : [])));
    if (next.size === floored.size && [...next].every((index) => floored.has(index))) return unit;
    floored = next;
  }
  return 0;
}

export function layoutFlow(counts: ApplicationFlowCounts, width: number, height: number, scale: number): FlowLayout {
  const s = scale;
  const nodeWidth = 16 * s;
  const gap = 24 * s;
  const header = 34 * s;
  const pad = 6 * s;
  const minSlot = 24 * s;
  const twoLineSlot = 54 * s;

  const x1 = 0;
  const x2 = Math.round(width * 0.43);
  const x3 = Math.round(width * 0.74);
  const top = pad + header;
  const available = height - top - pad;

  const first = FIRST_COLUMN.filter((key) => counts[key] > 0);
  const second = SECOND_COLUMN.filter((key) => counts[key] > 0);

  // One unit for the whole diagram, taken from the first column (it holds every application);
  // the second column is a subset of one node, so it always fits at the same unit.
  const unit = solveUnit(
    first.map((key) => counts[key]),
    available,
    minSlot,
    gap,
  );
  const minBar = 2 * s;
  const barHeight = (count: number) => Math.max(minBar, count * unit);

  const nodes: FlowNode[] = [];
  const bands: FlowBand[] = [];

  // Column 0: every application, one bar centred on the first column's span.
  const firstSpan =
    first.reduce((sum, key) => sum + Math.max(counts[key] * unit, minSlot), 0) + gap * Math.max(0, first.length - 1);
  const totalHeight = counts.total * unit;
  const totalY = top + (firstSpan - totalHeight) / 2;
  nodes.push({
    key: "total",
    column: 0,
    count: counts.total,
    x: x1,
    y: round(totalY),
    width: nodeWidth,
    height: round(totalHeight),
    slotMid: round(totalY + totalHeight / 2),
    twoLine: true,
  });

  // Column 1, and the ribbons into it from the total bar.
  let slotTop = top;
  let source = totalY;
  for (const key of first) {
    const slot = Math.max(counts[key] * unit, minSlot);
    const h = barHeight(counts[key]);
    const y = slotTop + (slot - h) / 2;
    nodes.push({
      key,
      column: 1,
      count: counts[key],
      x: x2,
      y: round(y),
      width: nodeWidth,
      height: round(h),
      slotMid: round(slotTop + slot / 2),
      twoLine: slot >= twoLineSlot,
    });
    const share = counts[key] * unit;
    bands.push({ from: "total", to: key, d: ribbon(x1 + nodeWidth, source, source + share, x2, y, y + h), tone: FLOW_TONE[key] });
    source += share;
    slotTop += slot + gap;
  }

  // Column 2, out of the interviewed node, centred on it and kept inside the diagram.
  const interviewed = nodes.find((node) => node.key === "interviewed");
  if (interviewed && second.length > 0) {
    const slots = second.map((key) => Math.max(counts[key] * unit, minSlot));
    const span = slots.reduce((sum, slot) => sum + slot, 0) + gap * (second.length - 1);
    let y3 = interviewed.y + interviewed.height / 2 - span / 2;
    y3 = Math.min(Math.max(y3, top), height - pad - span);

    let fromY = interviewed.y;
    second.forEach((key, index) => {
      const slot = slots[index];
      const h = barHeight(counts[key]);
      const y = y3 + (slot - h) / 2;
      nodes.push({
        key,
        column: 2,
        count: counts[key],
        x: x3,
        y: round(y),
        width: nodeWidth,
        height: round(h),
        slotMid: round(y3 + slot / 2),
        twoLine: false,
      });
      const share = counts[key] * unit;
      bands.push({
        from: "interviewed",
        to: key,
        d: ribbon(x2 + nodeWidth, fromY, fromY + share, x3, y, y + h),
        tone: FLOW_TONE[key],
      });
      fromY += share;
      y3 += slot + gap;
    });
  }

  return {
    width,
    height,
    scale: s,
    columnX: [x1, x2, x3],
    top,
    hasSecondColumn: second.length > 0,
    nodes,
    bands,
  };
}
