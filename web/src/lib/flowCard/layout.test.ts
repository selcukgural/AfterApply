import { describe, expect, it } from "vitest";
import type { ApplicationFlowCounts } from "@/types/api";
import { FLOW_FORMAT_SPECS, FLOW_FORMATS, scaleFlowFormatSpec } from "./formats";
import { layoutFlow } from "./layout";

const counts: ApplicationFlowCounts = {
  total: 86, unanswered: 47, awaitingReply: 0, rejectedBeforeInterview: 21, inScreening: 0,
  withdrawnBeforeInterview: 0, interviewed: 18, offer: 2, interviewInProgress: 4,
  rejectedAfterInterview: 9, silentAfterInterview: 3, withdrawnAfterInterview: 0,
};

// Every node present, with a lone 1 in the smallest ones.
const crowded: ApplicationFlowCounts = {
  total: 120, unanswered: 60, awaitingReply: 1, rejectedBeforeInterview: 30, inScreening: 1,
  withdrawnBeforeInterview: 1, interviewed: 27, offer: 1, interviewInProgress: 1,
  rejectedAfterInterview: 20, silentAfterInterview: 4, withdrawnAfterInterview: 1,
};

describe("the flow diagram layout", () => {
  it("draws a node only for counts above zero, and a ribbon into each", () => {
    const layout = layoutFlow(counts, 1088, 350, 1);
    expect(layout.nodes.map((node) => node.key)).toEqual([
      "total", "unanswered", "rejectedBeforeInterview", "interviewed",
      "offer", "interviewInProgress", "rejectedAfterInterview", "silentAfterInterview",
    ]);
    expect(layout.bands).toHaveLength(7);
  });

  it.each(FLOW_FORMATS)("keeps every node and label slot inside the diagram at the %s size", (format) => {
    const spec = FLOW_FORMAT_SPECS[format];
    for (const data of [counts, crowded]) {
      const layout = layoutFlow(data, spec.diagramWidth, spec.diagramHeight, spec.diagramScale);
      for (const node of layout.nodes) {
        expect(node.y).toBeGreaterThanOrEqual(layout.top - 0.5);
        expect(node.y + node.height).toBeLessThanOrEqual(spec.diagramHeight + 0.5);
        expect(node.x + node.width).toBeLessThanOrEqual(spec.diagramWidth);
      }
    }
  });

  it("gives each small node a slot of its own, so labels never overlap", () => {
    const spec = FLOW_FORMAT_SPECS.link;
    const layout = layoutFlow(crowded, spec.diagramWidth, spec.diagramHeight, spec.diagramScale);
    for (const column of [1, 2] as const) {
      const mids = layout.nodes.filter((node) => node.column === column).map((node) => node.slotMid);
      for (let i = 1; i < mids.length; i++) expect(mids[i] - mids[i - 1]).toBeGreaterThanOrEqual(24);
    }
  });

  it("uses one scale for the whole diagram: the total bar is as tall as its parts", () => {
    const layout = layoutFlow(counts, 1088, 350, 1);
    const total = layout.nodes.find((node) => node.key === "total")!;
    const unanswered = layout.nodes.find((node) => node.key === "unanswered")!;
    expect(unanswered.height / total.height).toBeCloseTo(47 / 86, 2);
  });

  it("has no second column when nothing reached an interview", () => {
    const layout = layoutFlow(
      { ...counts, unanswered: 65, interviewed: 0, offer: 0, interviewInProgress: 0, rejectedAfterInterview: 0, silentAfterInterview: 0 },
      1088, 350, 1,
    );
    expect(layout.hasSecondColumn).toBe(false);
    expect(layout.nodes.some((node) => node.column === 2)).toBe(false);
  });

  it("doubles every measure of a format for the high-density image, and nothing else", () => {
    const doubled = scaleFlowFormatSpec(FLOW_FORMAT_SPECS.story, 2);
    expect(doubled.width).toBe(2160);
    expect(doubled.padding).toEqual([520, 128, 580, 128]);
    expect(doubled.diagramScale).toBe(2.7);
    expect(doubled.footerStacked).toBe(true);
    expect(scaleFlowFormatSpec(FLOW_FORMAT_SPECS.link, 1)).toBe(FLOW_FORMAT_SPECS.link);
  });
});
