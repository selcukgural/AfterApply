import type {
  CompanySalaryRequest,
  EmploymentType,
  ExperienceBand,
  MyCompanySalary,
  OccupationRef,
  SalaryCurrency,
  SalaryEmploymentStatus,
} from "@/types/api";

// Mirrors CompanySalaryEntry's constants and CompanySalaryRequestValidator on the server. The
// server is the one that decides; these exist so the form can say the same thing before a round
// trip.
export const YEARS_MIN = 0;
export const YEARS_MAX = 50;
export const AMOUNT_MIN = 1;
export const AMOUNT_MAX = 10_000_000;

export const SALARY_CURRENCIES: readonly SalaryCurrency[] = ["TRY", "EUR", "USD", "GBP"];
export const SALARY_EMPLOYMENT_STATUSES: readonly SalaryEmploymentStatus[] = ["CurrentEmployee", "FormerEmployee"];
export const SALARY_EMPLOYMENT_TYPES: readonly EmploymentType[] = [
  "FullTime",
  "PartTime",
  "Contract",
  "Internship",
  "Freelance",
  "Temporary",
];
export const EXPERIENCE_BANDS: readonly ExperienceBand[] = ["ZeroToOne", "TwoToFour", "FiveToNine", "TenPlus"];

/** What the form holds: strings for the numeric fields (what an input gives back), the bonus
 *  answer as a tri-state so "not answered yet" is distinct from "no", and the occupation as the
 *  picked catalogue id plus the text the input shows — typing after a pick clears the id, because
 *  what was typed is not what was picked. */
export interface SalaryDraft {
  occupationId: string | null;
  occupationLabel: string;
  yearsOfExperience: string;
  employmentType: EmploymentType | "";
  employmentStatus: SalaryEmploymentStatus | "";
  monthlyNetAmount: string;
  currency: SalaryCurrency;
  hasBonus: boolean | null;
  annualBonusAmount: string;
}

export const EMPTY_SALARY_DRAFT: SalaryDraft = {
  occupationId: null,
  occupationLabel: "",
  yearsOfExperience: "",
  employmentType: "",
  employmentStatus: "",
  monthlyNetAmount: "",
  currency: "TRY",
  hasBonus: null,
  annualBonusAmount: "",
};

/** Translation keys under `companySalaries.form.problems`. */
export type SalaryDraftProblem =
  | "occupationRequired"
  | "yearsInvalid"
  | "employmentTypeRequired"
  | "employmentStatusRequired"
  | "amountInvalid"
  | "bonusAnswerRequired"
  | "bonusAmountInvalid";

export type SalaryDraftField =
  | "occupation"
  | "yearsOfExperience"
  | "employmentType"
  | "employmentStatus"
  | "monthlyNetAmount"
  | "hasBonus"
  | "annualBonusAmount";

/** "45.000", "45 000", "45000,50" and "45000.50" all read as the number a person meant. A
 *  single separator followed by exactly two digits is a decimal mark; every other separator is
 *  a thousands group. Returns null for anything that is not a number. */
export function parseAmount(raw: string): number | null {
  const trimmed = raw.trim().replace(/\s/g, "");
  if (trimmed === "" || !/^[0-9.,]+$/.test(trimmed)) return null;

  // A trailing separator with one or two digits is the decimal mark; three digits is a group.
  const decimal = /^(.+?)[.,](\d{1,2})$/.exec(trimmed);
  const integerText = decimal ? decimal[1] : trimmed;
  const fraction = decimal ? decimal[2] : "";

  // The integer part is plain digits, or digits in groups of three behind one kind of separator.
  const grouped = /^\d{1,3}(?:([.,])\d{3})(?:\1\d{3})*$/.test(integerText);
  if (!grouped && !/^\d+$/.test(integerText)) return null;
  // "45.000,50" is fine; "45.000.50" would read as a three-way group and is refused above.
  if (grouped && decimal && integerText.includes(trimmed[trimmed.length - fraction.length - 1])) return null;

  const value = Number(`${integerText.replace(/[.,]/g, "")}${fraction ? `.${fraction}` : ""}`);
  return Number.isFinite(value) ? value : null;
}

function isAmountInRange(value: number | null): value is number {
  return value !== null && value >= AMOUNT_MIN && value <= AMOUNT_MAX;
}

/** Every problem at once, keyed by field, so the form can mark each field rather than the first. */
export function validateSalaryDraft(draft: SalaryDraft): Partial<Record<SalaryDraftField, SalaryDraftProblem>> {
  const problems: Partial<Record<SalaryDraftField, SalaryDraftProblem>> = {};

  if (!draft.occupationId) {
    problems.occupation = "occupationRequired";
  }

  const years = draft.yearsOfExperience.trim() === "" ? NaN : Number(draft.yearsOfExperience);
  if (!Number.isInteger(years) || years < YEARS_MIN || years > YEARS_MAX) {
    problems.yearsOfExperience = "yearsInvalid";
  }

  if (draft.employmentType === "") problems.employmentType = "employmentTypeRequired";
  if (draft.employmentStatus === "") problems.employmentStatus = "employmentStatusRequired";

  if (!isAmountInRange(parseAmount(draft.monthlyNetAmount))) {
    problems.monthlyNetAmount = "amountInvalid";
  }

  if (draft.hasBonus === null) {
    problems.hasBonus = "bonusAnswerRequired";
  } else if (draft.hasBonus && !isAmountInRange(parseAmount(draft.annualBonusAmount))) {
    problems.annualBonusAmount = "bonusAmountInvalid";
  }

  return problems;
}

/** Only call after validateSalaryDraft returned no problems — the casts rely on it. */
export function buildSalaryRequest(draft: SalaryDraft): CompanySalaryRequest {
  return {
    occupationId: draft.occupationId as string,
    yearsOfExperience: Number(draft.yearsOfExperience),
    employmentType: draft.employmentType as EmploymentType,
    employmentStatus: draft.employmentStatus as SalaryEmploymentStatus,
    monthlyNetAmount: parseAmount(draft.monthlyNetAmount) as number,
    currency: draft.currency,
    hasBonus: draft.hasBonus === true,
    annualBonusAmount: draft.hasBonus ? (parseAmount(draft.annualBonusAmount) as number) : null,
  };
}

/** The draft an edit form starts from; the label is in the reader's language. */
export function draftFromSalary(entry: MyCompanySalary, locale: string): SalaryDraft {
  return {
    occupationId: entry.occupation.id,
    occupationLabel: occupationName(entry.occupation, locale),
    yearsOfExperience: String(entry.yearsOfExperience),
    employmentType: entry.employmentType,
    employmentStatus: entry.employmentStatus,
    monthlyNetAmount: String(entry.monthlyNetAmount),
    currency: entry.currency,
    hasBonus: entry.annualBonusAmount !== null,
    annualBonusAmount: entry.annualBonusAmount === null ? "" : String(entry.annualBonusAmount),
  };
}

/** The catalogue name for the reader's language; the other one is what the typeahead shows
 *  underneath. Anything that is not Turkish gets English. */
export function occupationName(occupation: OccupationRef, locale: string): string {
  return locale === "tr" ? occupation.nameTr : occupation.nameEn;
}

export function occupationOtherName(occupation: OccupationRef, locale: string): string {
  return locale === "tr" ? occupation.nameEn : occupation.nameTr;
}

/** The reader's band for an exact number of years — the same cut-offs as ExperienceBands.From. */
export function bandForYears(years: number): ExperienceBand {
  if (years <= 1) return "ZeroToOne";
  if (years <= 4) return "TwoToFour";
  if (years <= 9) return "FiveToNine";
  return "TenPlus";
}

/** "95.000 ₺" in tr, "₺95,000" in en — whole units, no decimals: a salary is read at a glance. */
export function formatAmount(locale: string, amount: number, currency: SalaryCurrency): string {
  return new Intl.NumberFormat(locale, { style: "currency", currency, maximumFractionDigits: 0 }).format(amount);
}

/** "Eylül 2026" / "September 2026" for a yyyy-MM. */
export function formatSalaryMonth(submittedMonth: string, locale: string): string {
  const [year, month] = submittedMonth.split("-").map(Number);
  if (!year || !month) return submittedMonth;
  return new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(year, month - 1, 1)),
  );
}
