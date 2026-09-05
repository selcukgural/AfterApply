import { describe, expect, it } from "vitest";
import { formatCount, formatDays, formatRate, formatWeekStart } from "@/lib/dashboard/format";

describe("formatRate", () => {
  it("keeps the decimal the API already computed instead of rounding it away", () => {
    // The old dashboard ran this through Math.round() and rendered a real 0.4% as "0%".
    expect(formatRate(0.4, "en")).toBe("0.4%");
    expect(formatRate(0.4, "tr")).toBe("%0,4");
  });

  it("puts the percent sign where each locale wants it", () => {
    expect(formatRate(63, "en")).toBe("63.0%");
    expect(formatRate(63, "tr")).toBe("%63,0");
  });

  it("treats its argument as 0-100, not as a fraction", () => {
    expect(formatRate(100, "en")).toBe("100.0%");
  });
});

describe("formatCount", () => {
  it("groups thousands per locale", () => {
    expect(formatCount(1187, "en")).toBe("1,187");
    expect(formatCount(1187, "tr")).toBe("1.187");
  });
});

describe("formatDays", () => {
  it("always shows one decimal so 8 and 8.3 line up in the same row", () => {
    expect(formatDays(8, "en")).toBe("8.0");
    expect(formatDays(0.5, "tr")).toBe("0,5");
  });
});

describe("formatWeekStart", () => {
  it("reads the date as UTC, so a bucket never slips a day west of Greenwich", () => {
    expect(formatWeekStart("2026-08-31", "en")).toBe("Aug 31");
  });
});
