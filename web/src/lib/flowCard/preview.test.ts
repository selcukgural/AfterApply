import { describe, expect, it } from "vitest";
import { FLOW_FORMATS, FLOW_FORMAT_SPECS } from "./formats";
import { previewScale } from "./preview";

describe("previewScale", () => {
  // The dialog's box: `h-80` less `p-3`, in a wide modal.
  const box = { width: 600, height: 296 };

  it("fits a wide card by its height and a tall one by its height too, as object-contain would", () => {
    expect(previewScale(box, "link")).toBeCloseTo(296 / 630);
    expect(previewScale(box, "portrait")).toBeCloseTo(296 / 1350);
    expect(previewScale(box, "story")).toBeCloseTo(296 / 1920);
  });

  it("fits by width when the box is narrow (a phone)", () => {
    expect(previewScale({ width: 300, height: 296 }, "x")).toBeCloseTo(300 / 1600);
  });

  it("never leaves the box, whatever the format", () => {
    for (const format of FLOW_FORMATS) {
      const scale = previewScale(box, format);
      const { width, height } = FLOW_FORMAT_SPECS[format];
      expect(width * scale).toBeLessThanOrEqual(box.width + 1e-9);
      expect(height * scale).toBeLessThanOrEqual(box.height + 1e-9);
    }
  });

  it("never enlarges past the card's own size", () => {
    expect(previewScale({ width: 5000, height: 5000 }, "link")).toBe(1);
  });

  it("draws nothing for a box that has not been measured", () => {
    expect(previewScale({ width: 0, height: 0 }, "link")).toBe(0);
    expect(previewScale({ width: 600, height: 0 }, "portrait")).toBe(0);
  });
});
