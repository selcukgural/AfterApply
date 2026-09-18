import { describe, expect, it } from "vitest";
import { progressKey, rateChip } from "./tone";

describe("progressKey", () => {
  it("names both when both are in motion", () => {
    expect(progressKey(2, 1)).toBe("both");
  });

  it("names only the side that is in motion", () => {
    expect(progressKey(2, 0)).toBe("interviews");
    expect(progressKey(0, 1)).toBe("offers");
  });

  it("says nothing when nothing is in motion — a zero is not a sentence", () => {
    expect(progressKey(0, 0)).toBeNull();
  });
});

describe("rateChip", () => {
  it("shows the rate when there is a count behind it", () => {
    expect(rateChip(3, "12% rate")).toBe("12% rate");
  });

  it("drops the chip at zero rather than saying 0% twice", () => {
    expect(rateChip(0, "0% rate")).toBeUndefined();
  });
});
