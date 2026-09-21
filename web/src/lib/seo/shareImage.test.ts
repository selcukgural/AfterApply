import { describe, expect, it } from "vitest";
import { coverIsShareImage, shareImageVerdict } from "./shareImage";

describe("shareImageVerdict", () => {
  it("takes a cover that is at least the card's size and roughly its ratio", () => {
    expect(shareImageVerdict({ width: 1200, height: 630 })).toBe("cover");
    expect(shareImageVerdict({ width: 1600, height: 900 })).toBe("cover");
    expect(shareImageVerdict({ width: 2400, height: 1260 })).toBe("cover");
  });

  it("refuses a small cover, a square or tall one, and one of unknown size", () => {
    expect(shareImageVerdict({ width: 640, height: 480 })).toBe("tooSmall");
    expect(shareImageVerdict({ width: 1200, height: 600 })).toBe("tooSmall");
    expect(shareImageVerdict({ width: 1200, height: 1200 })).toBe("wrongRatio");
    expect(shareImageVerdict({ width: 1200, height: 2000 })).toBe("wrongRatio");
    expect(shareImageVerdict({ width: 3000, height: 630 })).toBe("wrongRatio");
    expect(shareImageVerdict({ width: null, height: null })).toBe("unknownSize");
  });

  it("is null without a cover at all", () => {
    expect(shareImageVerdict(null)).toBeNull();
    expect(shareImageVerdict(undefined)).toBeNull();
    expect(coverIsShareImage(null)).toBe(false);
    expect(coverIsShareImage({ width: 1200, height: 630 })).toBe(true);
  });
});
