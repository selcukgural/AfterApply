import { z } from "zod";
import {
  AMOUNT_MAX,
  AMOUNT_MIN,
  PERIOD_MIN_YEAR,
  SALARY_CURRENCIES,
  SALARY_EMPLOYMENT_STATUSES,
  SALARY_EMPLOYMENT_TYPES,
  YEARS_MAX,
  YEARS_MIN,
} from "@/lib/companySalaries/salaryDraft";

/** The same rules as CompanySalaryRequestValidator on the server, as a zod schema for the request
 *  shape. The form validates its draft with validateSalaryDraft (per-field keys); this is the belt
 *  to those braces at the point the request is built. */
export function createCompanySalarySchema(t: (key: string) => string, currentYear = new Date().getFullYear()) {
  const amount = z.number().min(AMOUNT_MIN, t("salaryAmountInvalid")).max(AMOUNT_MAX, t("salaryAmountInvalid"));
  const year = z.number().int().min(PERIOD_MIN_YEAR, t("salaryPeriodYearInvalid")).max(currentYear, t("salaryPeriodYearInvalid"));
  return z
    .object({
      occupationId: z.string().uuid(t("salaryOccupationRequired")),
      yearsOfExperience: z.number().int().min(YEARS_MIN, t("salaryYearsInvalid")).max(YEARS_MAX, t("salaryYearsInvalid")),
      employmentType: z.enum(SALARY_EMPLOYMENT_TYPES as [string, ...string[]], { message: t("employmentTypeRequired") }),
      employmentStatus: z.enum(SALARY_EMPLOYMENT_STATUSES as [string, ...string[]], { message: t("employmentStatusRequired") }),
      monthlyNetAmount: amount,
      currency: z.enum(SALARY_CURRENCIES as [string, ...string[]], { message: t("salaryCurrencyRequired") }),
      hasBonus: z.boolean(),
      annualBonusAmount: amount.nullable(),
      periodStartYear: year,
      periodEndYear: year.nullable(),
    })
    .refine((r) => (r.hasBonus ? r.annualBonusAmount !== null : r.annualBonusAmount === null), {
      message: t("salaryBonusAmountInvalid"),
      path: ["annualBonusAmount"],
    })
    // A former employee closes the period; a current one leaves it open — and never backwards.
    .refine(
      (r) =>
        r.employmentStatus === "FormerEmployee"
          ? r.periodEndYear !== null && r.periodEndYear >= r.periodStartYear
          : r.periodEndYear === null,
      { message: t("salaryPeriodEndInvalid"), path: ["periodEndYear"] },
    );
}
