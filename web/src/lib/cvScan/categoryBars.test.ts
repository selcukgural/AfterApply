import { describe, expect, it } from "vitest";
import {
  BAR_GROW_MS,
  BAR_STAGGER_MS,
  barDelayMs,
  barProgress,
  barsTotalMs,
  categoryBand,
  categoryFillPercent,
  countUpValue,
  easeOutCubic,
} from "./categoryBars";

describe("categoryBand", () => {
  it("applies the headline's thresholds to the category's own ratio", () => {
    // 32/40 is 80%: the same line that makes an 80 headline "good".
    expect(categoryBand(32, 40)).toBe("good");
    expect(categoryBand(31, 40)).toBe("fair");
    expect(categoryBand(22, 40)).toBe("fair");
    expect(categoryBand(21, 40)).toBe("poor");
    expect(categoryBand(15, 15)).toBe("good");
    expect(categoryBand(0, 20)).toBe("poor");
  });

  it("does not round a category up into the next band", () => {
    // 19/24 is 79.2%: rounding to 80 first would paint it good.
    expect(categoryBand(19, 24)).toBe("fair");
  });
});

describe("categoryFillPercent", () => {
  it("is the subtotal over the weight, so the bar is the number again", () => {
    expect(categoryFillPercent(32, 40)).toBe(80);
    expect(categoryFillPercent(15, 15)).toBe(100);
    expect(categoryFillPercent(0, 20)).toBe(0);
  });

  it("never draws past the track or below it, whatever the response says", () => {
    expect(categoryFillPercent(50, 40)).toBe(100);
    expect(categoryFillPercent(-3, 40)).toBe(0);
    expect(categoryFillPercent(5, 0)).toBe(0);
  });
});

describe("timing", () => {
  it("staggers bars and ends when the last one has finished growing", () => {
    expect(barDelayMs(0)).toBe(0);
    expect(barDelayMs(3)).toBe(3 * BAR_STAGGER_MS);
    expect(barsTotalMs(4)).toBe(3 * BAR_STAGGER_MS + BAR_GROW_MS);
    expect(barsTotalMs(0)).toBe(0);
  });

  it("eases out: fast at the start, settled at the end, clamped outside 0..1", () => {
    expect(easeOutCubic(0)).toBe(0);
    expect(easeOutCubic(1)).toBe(1);
    expect(easeOutCubic(0.5)).toBeGreaterThan(0.5);
    expect(easeOutCubic(-1)).toBe(0);
    expect(easeOutCubic(2)).toBe(1);
  });

  it("holds a bar at zero until its turn and at one after its grow time", () => {
    expect(barProgress(2, 2 * BAR_STAGGER_MS - 1)).toBe(0);
    expect(barProgress(2, 2 * BAR_STAGGER_MS)).toBe(0);
    expect(barProgress(2, 2 * BAR_STAGGER_MS + BAR_GROW_MS)).toBe(1);
    expect(barProgress(2, 2 * BAR_STAGGER_MS + BAR_GROW_MS / 2)).toBeGreaterThan(0.5);
  });
});

describe("countUpValue", () => {
  it("starts at zero and lands exactly on the response's subtotal", () => {
    expect(countUpValue(32, 0, 0)).toBe(0);
    expect(countUpValue(32, 0, BAR_GROW_MS)).toBe(32);
    expect(countUpValue(32, 0, BAR_GROW_MS * 10)).toBe(32);
  });

  it("is monotonic on the way up and prints whole points only", () => {
    let previous = -1;
    for (let ms = 0; ms <= BAR_GROW_MS; ms += 50) {
      const value = countUpValue(22, 0, ms);
      expect(Number.isInteger(value)).toBe(true);
      expect(value).toBeGreaterThanOrEqual(previous);
      previous = value;
    }
  });

  it("shows every bar's final figure once the whole sequence is over", () => {
    const total = barsTotalMs(4);
    expect([32, 22, 15, 9].map((score, index) => countUpValue(score, index, total))).toEqual([32, 22, 15, 9]);
  });

  it("shows the final figures at the elapsed value the server renders with", () => {
    // useElapsedMs reports barsTotalMs on the server and under reduced motion: the static HTML
    // must carry the real subtotals, not zeros.
    expect(countUpValue(9, 3, barsTotalMs(4))).toBe(9);
  });
});
