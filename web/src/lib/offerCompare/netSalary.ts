import type { PayrollYear, TaxBracket } from "./payroll";

/** Income tax on a cumulative base under the tariff — the whole year's tax up to `base`. */
export function tariffTax(base: number, brackets: readonly TaxBracket[]): number {
  let tax = 0;
  let floor = 0;
  for (const { upTo, rate } of brackets) {
    if (base <= floor) break;
    tax += (Math.min(base, upTo) - floor) * rate;
    floor = upTo;
  }
  return tax;
}

/**
 * To the kuruş, half up — a payslip's own rounding. Every component is rounded before the net is
 * taken from them, so the parts add up to the gross and the minimum-wage exemption comes out as
 * the published 4.211,33 rather than 4.211,325 (validated against three public calculators,
 * 2026-09-24). The nudge keeps a …,xx5 that floating point stores as …,xx4999 rounding up.
 */
export function kurus(value: number): number {
  return Math.round(value * 100 + 1e-6) / 100;
}

/** One month's pay as the payroll sees it. */
export interface PayMonth {
  /** Cash wage paid that month, a bonus included. */
  gross: number;
  /** Non-cash pay taxed as wage — the meal card over the exemption, employer-paid insurance and BES. */
  taxableBenefits: number;
  /** The part of those benefits that also enters the SGK base. */
  sgkBenefits: number;
  /** Employer-paid health insurance deductible from the income-tax base (GVK 63/1-3). */
  insuranceDeduction: number;
}

export interface NetMonth {
  net: number;
  sgk: number;
  incomeTax: number;
  stampDuty: number;
}

/**
 * Gross → net for a calendar year, month by month, the way a Turkish payroll computes it:
 *
 * - SGK employee share and unemployment insurance on the wage up to the ceiling;
 * - income tax on the **cumulative** base, so the same gross nets less as the year moves into
 *   higher brackets — the reason a gross offer's January and December differ;
 * - less the minimum-wage exemption: the tax the tariff would take from the minimum wage's own
 *   cumulative base that month (GVK 23/18), never more than the tax itself;
 * - stamp duty on everything above the gross minimum wage.
 *
 * `months` is January first; fewer than twelve is a partial year from January.
 */
export function netYear(months: readonly PayMonth[], year: PayrollYear): NetMonth[] {
  const employeeRate = year.sgkEmployeeRate + year.unemploymentEmployeeRate;
  const minimumWageBase = year.minimumWageGross * (1 - employeeRate);
  let cumulative = 0;
  let cumulativeMinimum = 0;

  return months.map((month) => {
    if (month.gross <= 0) return { net: 0, sgk: 0, incomeTax: 0, stampDuty: 0 };

    const sgk = kurus(Math.min(month.gross + month.sgkBenefits, year.sgkCeiling) * employeeRate);
    const base = Math.max(0, month.gross + month.taxableBenefits - sgk - month.insuranceDeduction);
    const tax = kurus(tariffTax(cumulative + base, year.brackets) - tariffTax(cumulative, year.brackets));
    const exemption = kurus(
      tariffTax(cumulativeMinimum + minimumWageBase, year.brackets) - tariffTax(cumulativeMinimum, year.brackets),
    );
    cumulative += base;
    cumulativeMinimum += minimumWageBase;

    const incomeTax = Math.max(0, kurus(tax - exemption));
    // The exemption is the minimum wage's own duty (250,70 in 2026), taken off the rounded duty.
    const stampDuty = Math.max(
      0,
      kurus(kurus((month.gross + month.taxableBenefits) * year.stampDutyRate) - kurus(year.minimumWageGross * year.stampDutyRate)),
    );
    return { net: kurus(month.gross - sgk - incomeTax - stampDuty), sgk, incomeTax, stampDuty };
  });
}
