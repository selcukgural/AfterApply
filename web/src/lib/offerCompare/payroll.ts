/**
 * The Turkish payroll figures the offer comparison computes with — one calendar year's worth, every
 * number traced to the text that sets it. Nothing here is estimated; a figure without a primary
 * source does not belong in this file.
 *
 * **Refreshing it every January** (the page says the figures are the year's, so a stale year is a
 * visible lie — `payroll.test.ts` fails from 15 January of the next year until this is done):
 *
 * 1. Tax brackets: the Gelir Vergisi Genel Tebliği published around 30–31 December (Seri No 332 for
 *    2026, 329 for 2025). Take the wage tariff from the tebliğ itself — the brackets are revalued
 *    by the yeniden değerleme oranı but rounded, so multiplying last year's never gives the real
 *    figures (158.000 → 190.000 was +20 %, not the +25,49 % rate).
 * 2. Minimum wage: the Asgari Ücret Tespit Komisyonu decision in the Resmî Gazete (late December).
 *    The SGK floor (1×) and ceiling (currently 9×) and both minimum-wage exemptions follow from it.
 *    A mid-year raise is possible (none in 2026); it would need a dated period here.
 * 3. Meal allowance: the daily income-tax exemption is in the same tebliğ; the SGK exemption is
 *    revalued by the same rate from 2027 (Law 7577) and announced in SGK's January genelge.
 * 4. Rates (SGK shares, stamp duty, the 15–40 % tariff rates) change only by law — check for an
 *    omnibus law in December (Law 7566 raised the ceiling to 9× for 2026).
 */
export interface TaxBracket {
  /** The top of the bracket, on the year's cumulative taxable base; Infinity for the last. */
  upTo: number;
  rate: number;
}

export interface PayrollYear {
  year: number;
  /** GVK md. 103, the wage-income tariff (its third bracket ends higher than other income's). */
  brackets: readonly TaxBracket[];
  /** Monthly gross minimum wage — the base of both minimum-wage exemptions and the SGK floor. */
  minimumWageGross: number;
  /** SGK employee shares: long-term 9 % + general health 5 %, and unemployment insurance 1 %. */
  sgkEmployeeRate: number;
  unemploymentEmployeeRate: number;
  /** Monthly SGK ceiling (prime esas kazanç üst sınırı). */
  sgkCeiling: number;
  /** Stamp duty on wages, DVK (1) sayılı tablo. */
  stampDutyRate: number;
  /** GVK 23/8: meal allowance exempt from income tax, per day worked, excluding VAT. */
  mealTaxExemptPerDay: number;
  /** SGK 5510 md. 80/1-b: meal allowance exempt from the SGK base, per day worked — compared with
   *  the card's face value, VAT included (unlike the income-tax exemption). */
  mealSgkExemptPerDay: number;
  /** The VAT inside a meal card's face value — the card is quoted with it, the exemption without. */
  mealVatRate: number;
  /** GVK 63/1-3: private health insurance deductible up to this share of the month's wage… */
  insuranceDeductionRate: number;
  /** …and up to this much a year (the annual gross minimum wage). */
  insuranceDeductionAnnualCap: number;
  /** 5510 md. 80: employer-paid health insurance + BES stay out of the SGK base up to this much a month. */
  sgkBenefitExemptMonthly: number;
}

/**
 * 2026. Sources:
 * - Brackets: Gelir Vergisi Genel Tebliği Seri No 332, md. 3/3 — Resmî Gazete 31.12.2025,
 *   No 33124 (5. mükerrer). https://www.resmigazete.gov.tr/eskiler/2025/12/20251231M5-30.pdf
 * - Minimum wage 33.030 TL (1.101 TL/day, all of 2026): Asgari Ücret Tespit Komisyonu Kararı
 *   2025/1 — Resmî Gazete 26.12.2025, No 33119.
 * - SGK shares, floor and ceiling (9 × minimum wage, Law 7566, RG 19.12.2025 No 33112): SGK
 *   "Prime Esas Kazanç Miktarları" and "İşveren Prim Oranları" pages; SGK Genelgesi 2026/2.
 * - Minimum-wage income-tax and stamp-duty exemptions (GVK 23/18, DVK): GİB "Ücret Geliri Elde
 *   Edenler İçin Vergi Rehberi 2026", 4.3.1–4.3.3.
 * - Meal allowance: 300 TL/day income-tax exempt (Tebliğ 332 md. 3/2-c); SGK exemption 300 TL/day
 *   from 17.04.2026 (Law 7577, SGK Genelgesi 2026/12), 158 TL before. The comparison looks
 *   forward from today, so it uses the rule in force now.
 * - Insurance deduction: GVK 63/1-3 as summarised in the GİB 2026 wage guide, 4.1.3 and 4.4.1.
 */
export const PAYROLL_2026: PayrollYear = {
  year: 2026,
  brackets: [
    { upTo: 190_000, rate: 0.15 },
    { upTo: 400_000, rate: 0.2 },
    { upTo: 1_500_000, rate: 0.27 },
    { upTo: 5_300_000, rate: 0.35 },
    { upTo: Infinity, rate: 0.4 },
  ],
  minimumWageGross: 33_030,
  sgkEmployeeRate: 0.14,
  unemploymentEmployeeRate: 0.01,
  sgkCeiling: 297_270,
  stampDutyRate: 0.00759,
  mealTaxExemptPerDay: 300,
  mealSgkExemptPerDay: 300,
  mealVatRate: 0.1,
  insuranceDeductionRate: 0.15,
  insuranceDeductionAnnualCap: 33_030 * 12,
  sgkBenefitExemptMonthly: 33_030 * 0.3,
};

/** The year the page computes with. */
export const PAYROLL: PayrollYear = PAYROLL_2026;

/** Working days a month — the meal card is paid for them, and office days are counted from them. */
export const WORKDAYS_PER_MONTH = 22;
