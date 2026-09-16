/**
 * The decision half of swipe-to-dismiss, kept out of the component so it can be tested without a
 * DOM: given how far the pointer has travelled, is this a dismissal, a drag to spring back from, or
 * the page being scrolled? The component owns the pointer events and the animation.
 */

export type SwipeOutcome = "dismiss" | "reset" | "scroll";

export interface SwipeInput {
  /** Horizontal travel since pointer-down, in CSS px; sign is the direction. */
  dx: number;
  /** Vertical travel since pointer-down, in CSS px. */
  dy: number;
  /** The card's rendered width, so the threshold scales with it. */
  width: number;
}

/** A few px of vertical travel before the gesture is called a scroll, so a slightly diagonal
 *  thumb still swipes. */
const SCROLL_INTENT_PX = 8;

/** Never less than a deliberate flick, never more than the card's own width would make silly. */
const MIN_DISMISS_PX = 72;
const DISMISS_FRACTION = 0.3;

export function dismissThreshold(width: number): number {
  return Math.max(MIN_DISMISS_PX, width * DISMISS_FRACTION);
}

/**
 * True once the pointer has committed to a horizontal gesture: the card should follow the finger
 * and the browser should stop treating the movement as a scroll. Called on every move until it
 * says yes; a vertical-first move means the page scrolls and the card stays put.
 */
export function isHorizontalIntent(dx: number, dy: number): boolean {
  return Math.abs(dx) > Math.abs(dy) && Math.abs(dx) > SCROLL_INTENT_PX;
}

export function resolveSwipeGesture({ dx, dy, width }: SwipeInput): SwipeOutcome {
  if (Math.abs(dy) > SCROLL_INTENT_PX && Math.abs(dy) > Math.abs(dx)) {
    return "scroll";
  }
  return Math.abs(dx) >= dismissThreshold(width) ? "dismiss" : "reset";
}

/** The card follows the pointer and fades as it goes, fully transparent at ~1.5× the threshold. */
export function swipeStyle(dx: number, width: number): { transform: string; opacity: number } {
  const fade = Math.min(1, Math.abs(dx) / (dismissThreshold(width) * 1.5));
  return { transform: `translateX(${Math.round(dx)}px)`, opacity: 1 - fade * 0.8 };
}
