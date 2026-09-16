import { describe, expect, it } from "vitest";
import { dismissThreshold, isHorizontalIntent, resolveSwipeGesture, swipeStyle } from "./swipe";

describe("dismissThreshold", () => {
  it("is a share of the card's width on a wide card", () => {
    expect(dismissThreshold(640)).toBe(192);
  });

  it("never drops below a deliberate flick on a narrow card", () => {
    expect(dismissThreshold(200)).toBe(72);
  });
});

describe("isHorizontalIntent", () => {
  it("is not decided by the first few px, so a slightly diagonal thumb still swipes", () => {
    expect(isHorizontalIntent(5, 2)).toBe(false);
    expect(isHorizontalIntent(12, 6)).toBe(true);
  });

  it("stays false while the movement is mostly vertical", () => {
    expect(isHorizontalIntent(10, 14)).toBe(false);
  });
});

describe("resolveSwipeGesture", () => {
  it("dismisses past the threshold in either direction", () => {
    expect(resolveSwipeGesture({ dx: 200, dy: 3, width: 640 })).toBe("dismiss");
    expect(resolveSwipeGesture({ dx: -200, dy: 3, width: 640 })).toBe("dismiss");
  });

  it("springs back from a short drag", () => {
    expect(resolveSwipeGesture({ dx: 60, dy: 0, width: 640 })).toBe("reset");
    expect(resolveSwipeGesture({ dx: -191, dy: 0, width: 640 })).toBe("reset");
  });

  it("leaves a mostly vertical movement to the page's scroll, however far it went sideways", () => {
    expect(resolveSwipeGesture({ dx: 100, dy: 140, width: 640 })).toBe("scroll");
  });

  it("does not call a long swipe a scroll for a little vertical drift", () => {
    expect(resolveSwipeGesture({ dx: 250, dy: 40, width: 640 })).toBe("dismiss");
  });
});

describe("swipeStyle", () => {
  it("follows the pointer and keeps the card fully visible at rest", () => {
    expect(swipeStyle(0, 640)).toEqual({ transform: "translateX(0px)", opacity: 1 });
  });

  it("fades with distance but never to nothing while the pointer is still down", () => {
    const far = swipeStyle(-900, 640);
    expect(far.transform).toBe("translateX(-900px)");
    expect(far.opacity).toBeCloseTo(0.2);
    expect(swipeStyle(96, 640).opacity).toBeGreaterThan(far.opacity);
  });
});
