import { describe, expect, it } from "vitest";
import { kurus, netYear, tariffTax, type PayMonth } from "./netSalary";
import { PAYROLL_2026 } from "./payroll";

const cash = (gross: number): PayMonth => ({ gross, taxableBenefits: 0, sgkBenefits: 0, insuranceDeduction: 0 });
const year = (gross: number) => netYear(Array.from({ length: 12 }, () => cash(gross)), PAYROLL_2026);

describe("tariffTax", () => {
  // The tax on each bracket's top, as Tebliğ 332 prints it.
  it.each([
    [190_000, 28_500],
    [400_000, 70_500],
    [1_500_000, 367_500],
    [5_300_000, 1_697_500],
  ])("matches the tebliğ at %i", (base, tax) => {
    expect(tariffTax(base, PAYROLL_2026.brackets)).toBeCloseTo(tax, 6);
  });

  it("taxes nothing below zero and 40 % above the last bracket", () => {
    expect(tariffTax(0, PAYROLL_2026.brackets)).toBe(0);
    expect(tariffTax(5_400_000, PAYROLL_2026.brackets)).toBeCloseTo(1_697_500 + 40_000, 6);
  });
});

describe("netYear", () => {
  it("leaves the minimum wage untaxed: net is gross less the 15 % employee share", () => {
    for (const month of year(33_030)) {
      expect(month.incomeTax).toBe(0);
      expect(month.stampDuty).toBe(0);
      expect(month.net).toBeCloseTo(28_075.5, 2);
    }
  });

  it("computes a 90.000 gross January by hand", () => {
    // SGK 13.500; base 76.500 → tax 11.475; minimum-wage exemption 28.075,50 × 15 % = 4.211,33;
    // stamp (90.000 − 33.030) × 0,759 % = 432,40.
    const [january] = year(90_000);
    expect(january.sgk).toBeCloseTo(13_500, 2);
    expect(january.incomeTax).toBe(7_263.67);
    expect(january.stampDuty).toBe(432.4);
    expect(january.net).toBe(68_803.93);
  });

  it("nets less later in the year as the cumulative base climbs the brackets", () => {
    const months = year(90_000);
    expect(months[11].net).toBeLessThan(months[0].net);
    expect(Math.round(months[11].net)).toBe(61_028);
  });

  it("uses the minimum wage's own monthly exemption schedule (4.211,33 → 4.537,75 → 5.615,10)", () => {
    // At the minimum wage the tax and the exemption are the same number, so the tax before the
    // exemption is the published schedule.
    const months = netYear(Array.from({ length: 12 }, () => cash(33_030)), PAYROLL_2026);
    expect(months.every((month) => month.incomeTax === 0)).toBe(true);
    const base = 28_075.5;
    const exemption = (m: number) => tariffTax(base * (m + 1), PAYROLL_2026.brackets) - tariffTax(base * m, PAYROLL_2026.brackets);
    expect(exemption(0)).toBeCloseTo(4_211.33, 1);
    expect(exemption(6)).toBeCloseTo(4_537.75, 1);
    expect(exemption(7)).toBeCloseTo(5_615.1, 1);
  });

  it("stops the SGK share at the ceiling", () => {
    const [january] = year(400_000);
    expect(january.sgk).toBeCloseTo(297_270 * 0.15, 2);
  });

  // Validated 2026-09-24 against hesaplama.net, vergi.net and kariyer.net's calculators (2026,
  // single, no disability): every month within a kuruş. These are their January / July / December.
  it.each([
    [50_000, 40_207.53, 38_408.95, 36_511.3],
    [90_000, 68_803.93, 59_950.35, 61_027.7],
    [150_000, 111_698.53, 96_724.95, 95_402.3],
    [400_000, 295_253.63, 232_768.62, 233_845.97],
  ])("matches the public calculators for %i gross", (gross, jan, jul, dec) => {
    const months = year(gross);
    expect(months[0].net).toBeCloseTo(jan, 1);
    expect(months[6].net).toBeCloseTo(jul, 1);
    expect(months[11].net).toBeCloseTo(dec, 1);
  });

  it("adds up to the gross, to the kuruş", () => {
    for (const gross of [50_000, 90_000, 150_000, 400_000]) {
      for (const month of year(gross)) {
        expect(kurus(month.net + month.sgk + month.incomeTax + month.stampDuty)).toBe(gross);
      }
    }
  });

  it("rounds half up to the kuruş, floating point notwithstanding", () => {
    expect(kurus(4_211.325)).toBe(4_211.33);
    expect(kurus(0.005)).toBe(0.01);
    expect(kurus(10)).toBe(10);
  });

  it("pays nothing for an empty month", () => {
    expect(netYear([cash(0)], PAYROLL_2026)[0]).toEqual({ net: 0, sgk: 0, incomeTax: 0, stampDuty: 0 });
  });
});
