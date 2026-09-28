/**
 * The arithmetic behind the profile photo crop (DECISIONS.md 2026-09-28), kept apart from the
 * dialog so it can be tested without a DOM. The picture sits behind a square viewport of side
 * `viewport` (CSS px) and always covers it: at zoom 1 its shorter side fills the viewport exactly,
 * and no pan or zoom can uncover an edge.
 */

export const MIN_ZOOM = 1;
export const MAX_ZOOM = 3;
/** The square sent to the server. It re-encodes to 256 px; twice that keeps the downscale sharp. */
export const OUTPUT_SIZE = 512;

export interface CropState {
  /** The picture's top-left corner relative to the viewport's, in CSS px (≤ 0 once it covers). */
  x: number;
  y: number;
  zoom: number;
}

export interface SourceRect {
  sx: number;
  sy: number;
  size: number;
}

/** CSS px per picture pixel at the given zoom. */
export function displayScale(width: number, height: number, viewport: number, zoom: number): number {
  return (viewport / Math.min(width, height)) * zoom;
}

/** Keeps the picture covering the viewport. */
export function clampOffset(state: CropState, width: number, height: number, viewport: number): CropState {
  const scale = displayScale(width, height, viewport, state.zoom);
  const minX = viewport - width * scale;
  const minY = viewport - height * scale;
  return {
    zoom: state.zoom,
    x: Math.min(0, Math.max(minX, state.x)),
    y: Math.min(0, Math.max(minY, state.y)),
  };
}

/** The picture centred at zoom 1 — where a freshly chosen photo starts. */
export function initialCrop(width: number, height: number, viewport: number): CropState {
  const scale = displayScale(width, height, viewport, MIN_ZOOM);
  return { zoom: MIN_ZOOM, x: (viewport - width * scale) / 2, y: (viewport - height * scale) / 2 };
}

/** Moves the picture by a drag or an arrow key. */
export function pan(state: CropState, dx: number, dy: number, width: number, height: number, viewport: number): CropState {
  return clampOffset({ ...state, x: state.x + dx, y: state.y + dy }, width, height, viewport);
}

/** Zooms about the viewport's centre, so what the person is looking at stays in the middle. */
export function zoomTo(state: CropState, zoom: number, width: number, height: number, viewport: number): CropState {
  const next = Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, zoom));
  const before = displayScale(width, height, viewport, state.zoom);
  const after = displayScale(width, height, viewport, next);
  const centreX = (viewport / 2 - state.x) / before;
  const centreY = (viewport / 2 - state.y) / before;
  return clampOffset({ zoom: next, x: viewport / 2 - centreX * after, y: viewport / 2 - centreY * after }, width, height, viewport);
}

/** The square of the picture, in its own pixels, that the viewport shows. */
export function sourceRect(state: CropState, width: number, height: number, viewport: number): SourceRect {
  const scale = displayScale(width, height, viewport, state.zoom);
  const size = Math.min(viewport / scale, width, height);
  return {
    sx: Math.min(Math.max(0, -state.x / scale), width - size),
    sy: Math.min(Math.max(0, -state.y / scale), height - size),
    size,
  };
}
