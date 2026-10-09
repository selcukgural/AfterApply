import { describe, expect, it } from "vitest";
import { cvVersionDate } from "./cvVersion";

describe("cvVersionDate", () => {
  const now = new Date(2026, 9, 9, 12);

  it("names day and month for an upload this year", () => {
    expect(cvVersionDate(new Date(2026, 8, 12, 10).toISOString(), "tr", now)).toBe("12 Eylül");
    expect(cvVersionDate(new Date(2026, 8, 12, 10).toISOString(), "en", now)).toBe("September 12");
  });

  it("adds the year for an older upload", () => {
    expect(cvVersionDate(new Date(2025, 11, 3, 10).toISOString(), "tr", now)).toBe("3 Aralık 2025");
  });
});
