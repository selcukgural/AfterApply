import { describe, expect, it } from "vitest";
import { MAX_ZOOM, clampOffset, initialCrop, pan, sourceRect, zoomTo } from "./crop";

const VIEWPORT = 280;

describe("avatar crop", () => {
  it("starts with a landscape photo centred and its height filling the square", () => {
    const state = initialCrop(1200, 600, VIEWPORT);

    expect(state.zoom).toBe(1);
    expect(state.y).toBe(0);
    // 1200×600 drawn at 280/600: 560 wide, so 140 px hang over each side.
    expect(state.x).toBeCloseTo(-140);
    expect(sourceRect(state, 1200, 600, VIEWPORT)).toEqual({ sx: 300, sy: 0, size: 600 });
  });

  it("never lets a pan uncover an edge", () => {
    const start = initialCrop(1200, 600, VIEWPORT);

    expect(pan(start, 1000, 1000, 1200, 600, VIEWPORT)).toMatchObject({ x: 0, y: 0 });
    expect(pan(start, -1000, -1000, 1200, 600, VIEWPORT)).toMatchObject({ x: -280, y: 0 });
    expect(sourceRect(pan(start, -1000, 0, 1200, 600, VIEWPORT), 1200, 600, VIEWPORT).sx).toBeCloseTo(600);
  });

  it("zooms about the centre and stays within its range", () => {
    const start = initialCrop(800, 800, VIEWPORT);

    const zoomed = zoomTo(start, 2, 800, 800, VIEWPORT);
    const rect = sourceRect(zoomed, 800, 800, VIEWPORT);
    expect(rect.size).toBeCloseTo(400);
    // The middle of the photo stays in the middle of the square.
    expect(rect.sx + rect.size / 2).toBeCloseTo(400);
    expect(rect.sy + rect.size / 2).toBeCloseTo(400);

    expect(zoomTo(start, 99, 800, 800, VIEWPORT).zoom).toBe(MAX_ZOOM);
    expect(zoomTo(zoomed, 0.1, 800, 800, VIEWPORT)).toMatchObject({ zoom: 1, x: 0, y: 0 });
  });

  it("pulls an offset left over from a higher zoom back inside when zooming out", () => {
    const panned = pan(zoomTo(initialCrop(800, 800, VIEWPORT), 3, 800, 800, VIEWPORT), -5000, -5000, 800, 800, VIEWPORT);

    const out = clampOffset({ ...panned, zoom: 1 }, 800, 800, VIEWPORT);
    expect(out).toMatchObject({ x: 0, y: 0 });
  });
});
