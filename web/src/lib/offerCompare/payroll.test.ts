import { describe, expect, it } from "vitest";
import { PAYROLL } from "./payroll";

describe("PAYROLL", () => {
  // The page says it computes with this year's figures. From mid-January of the next year that is
  // no longer true until someone updates payroll.ts — the steps are in its header comment — so
  // this fails on purpose, rather than the page quietly computing last year's taxes.
  it("is the current year's figures (refresh payroll.ts every January)", () => {
    const now = new Date();
    const graceOver = now.getMonth() > 0 || now.getDate() > 15;
    const expected = graceOver ? now.getFullYear() : now.getFullYear() - 1;
    expect(PAYROLL.year, "payroll.ts is out of date — see its header comment").toBeGreaterThanOrEqual(expected);
  });

  it("keeps the ceiling at nine minimum wages and the exemptions tied to the minimum wage", () => {
    expect(PAYROLL.sgkCeiling).toBe(PAYROLL.minimumWageGross * 9);
    expect(PAYROLL.insuranceDeductionAnnualCap).toBe(PAYROLL.minimumWageGross * 12);
    expect(PAYROLL.sgkBenefitExemptMonthly).toBeCloseTo(PAYROLL.minimumWageGross * 0.3, 6);
  });
});
