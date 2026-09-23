import { describe, expect, it } from "vitest";
import { turkishPossessiveSuffix } from "./turkish";

describe("the Turkish possessive suffix after a numeral", () => {
  it.each([
    [0, "'ı"], [1, "'i"], [2, "'si"], [3, "'ü"], [4, "'ü"], [5, "'i"], [6, "'sı"], [7, "'si"], [8, "'i"], [9, "'u"],
    [10, "'u"], [20, "'si"], [30, "'u"], [40, "'ı"], [50, "'si"], [60, "'ı"], [70, "'i"], [80, "'i"], [90, "'ı"],
    [100, "'ü"], [200, "'ü"], [1000, "'i"], [3000, "'i"], [1_000_000, "'u"],
    [47, "'si"], [86, "'sı"], [53, "'ü"], [110, "'u"], [1204, "'ü"], [2500, "'ü"],
  ])("%i → %s", (value, suffix) => {
    expect(turkishPossessiveSuffix(value)).toBe(suffix);
  });
});
