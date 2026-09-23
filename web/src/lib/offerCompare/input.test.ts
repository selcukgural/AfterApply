import { describe, expect, it } from "vitest";
import { emptyDraft, exampleDrafts, formatLira, formatNumber, parseAmount, parseCount, toOffer } from "./input";

describe("parseAmount", () => {
  it("reads both locales' thousands separators and ignores the rest", () => {
    expect(parseAmount("90.000")).toBe(90_000);
    expect(parseAmount("90,000")).toBe(90_000);
    expect(parseAmount("₺ 1.500 ")).toBe(1_500);
    expect(parseAmount("")).toBe(0);
    expect(parseAmount("abc")).toBe(0);
  });

  it("caps a runaway number", () => {
    expect(parseAmount("9".repeat(30))).toBe(100_000_000);
  });
});

describe("parseCount", () => {
  it("takes the locale's decimal mark", () => {
    expect(parseCount("1,5", "tr", 12)).toBe(1.5);
    expect(parseCount("1.5", "en", 12)).toBe(1.5);
    expect(parseCount("2", "tr", 12)).toBe(2);
  });

  it("clamps into range and treats nonsense as zero", () => {
    expect(parseCount("9", "tr", 5)).toBe(5);
    expect(parseCount("", "tr", 5)).toBe(0);
    expect(parseCount("-", "en", 5)).toBe(0);
  });
});

describe("toOffer", () => {
  it("parses a draft and rounds remote days to whole days", () => {
    const offer = toOffer({ ...emptyDraft("C", "250"), salary: "65.000", bonusSalaries: "1,5", remoteDaysPerWeek: "2,6" }, "tr");
    expect(offer).toMatchObject({ name: "C", salary: 65_000, bonusSalaries: 1.5, remoteDaysPerWeek: 3, officeDayCost: 250 });
  });

  it("parses the examples it opens with", () => {
    const [a, b] = exampleDrafts("tr", ["Teklif A", "Teklif B"]).map((draft) => toOffer(draft, "tr"));
    expect(a).toMatchObject({ salary: 90_000, healthPerMonth: 1_500, remoteDaysPerWeek: 3 });
    expect(b).toMatchObject({ salary: 100_000, bonusSalaries: 2, remoteDaysPerWeek: 0 });
    const [enA] = exampleDrafts("en", ["Offer A", "Offer B"]).map((draft) => toOffer(draft, "en"));
    expect(enA.salary).toBe(90_000);
  });
});

describe("formatting", () => {
  it("writes lira the locale's way", () => {
    expect(formatNumber(68_803.9, "tr")).toBe("68.804");
    expect(formatNumber(68_803.9, "en")).toBe("68,804");
    expect(formatLira(52_011, "tr")).toBe("52.011 ₺");
    expect(formatLira(52_011, "en")).toBe("₺52,011");
  });
});
