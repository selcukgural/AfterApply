import { describe, expect, it } from "vitest";
import { formatMinor, formatMonth } from "./money";

describe("formatMinor", () => {
  it("renders kuruş as lira in each locale", () => {
    expect(formatMinor(29900, "TL", "tr").replace(/ /g, " ")).toBe("₺299,00");
    expect(formatMinor(29900, "TL", "en").replace(/ /g, " ")).toBe("₺299.00");
    expect(formatMinor(5, "TRY", "en")).toBe("₺0.05");
  });
});

describe("formatMonth", () => {
  it("names the month in the viewer's language and leaves garbage alone", () => {
    expect(formatMonth("2026-09", "tr")).toBe("Eylül 2026");
    expect(formatMonth("2026-09", "en")).toBe("September 2026");
    expect(formatMonth("nope", "en")).toBe("nope");
  });
});
