import { FLOW_FORMAT_SPECS, type FlowFormat } from "./formats";

/**
 * How far the card has to shrink to fit a preview box whole — the same fit `object-contain` gives
 * the PNG, so the draft drawn in the browser and the image that replaces it occupy exactly the
 * same rectangle. Never enlarged past its own size, and 0 for a box not measured yet (nothing
 * drawn rather than a card at full size for one frame).
 */
export function previewScale(box: { width: number; height: number }, format: FlowFormat): number {
  const { width, height } = FLOW_FORMAT_SPECS[format];
  if (box.width <= 0 || box.height <= 0) return 0;
  return Math.min(box.width / width, box.height / height, 1);
}
