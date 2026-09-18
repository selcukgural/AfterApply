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
export const PERIOD_MIN_YEAR = 1990;

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
 *  answer as a tri-state so "not answered yet" is distinct from "no", the period as two year
 *  strings (the end one only meaningful for a former employee), and the occupation as the
 *  picked catalogue id plus the text the input shows — typing after a pick clears the id, because
 *  what was typed is not what was picked. */
export interface SalaryDraft {
  occupationId: string | null;
  occupationLabel: string;
  yearsOfExperience: string;
  employmentType: EmploymentType | "";
  employmentStatus: SalaryEmploymentStatus | "";
  /** "" until picked; an entry written before the period existed starts here too. */
  periodStartYear: string;
  /** Only read when the status is FormerEmployee; the request carries null otherwise. */
  periodEndYear: string;
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
  periodStartYear: "",
  periodEndYear: "",
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
  | "periodStartRequired"
  | "periodStartInvalid"
  | "periodEndRequired"
  | "periodEndInvalid"
  | "periodEndBeforeStart"
  | "amountInvalid"
  | "bonusAnswerRequired"
  | "bonusAmountInvalid";

export type SalaryDraftField =
  | "occupation"
  | "yearsOfExperience"
  | "employmentType"
  | "employmentStatus"
  | "periodStartYear"
  | "periodEndYear"
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

/** The years the period selects offer: this year first, back to 1990. */
export function periodYearOptions(currentYear = new Date().getFullYear()): number[] {
  return Array.from({ length: currentYear - PERIOD_MIN_YEAR + 1 }, (_, i) => currentYear - i);
}

function parseYear(raw: string): number | null {
  const value = raw.trim() === "" ? NaN : Number(raw);
  return Number.isInteger(value) ? value : null;
}

/** Every problem at once, keyed by field, so the form can mark each field rather than the first.
 *  The year is a parameter so a test can pin "this year". */
export function validateSalaryDraft(
  draft: SalaryDraft,
  currentYear = new Date().getFullYear(),
): Partial<Record<SalaryDraftField, SalaryDraftProblem>> {
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

  // The period: a start year always; an end year only for a former employee, and not before
  // the start. A current employee's end is "still drawing it", which the request sends as null.
  const start = parseYear(draft.periodStartYear);
  if (draft.periodStartYear.trim() === "") {
    problems.periodStartYear = "periodStartRequired";
  } else if (start === null || start < PERIOD_MIN_YEAR || start > currentYear) {
    problems.periodStartYear = "periodStartInvalid";
  }
  if (draft.employmentStatus === "FormerEmployee") {
    const end = parseYear(draft.periodEndYear);
    if (draft.periodEndYear.trim() === "") {
      problems.periodEndYear = "periodEndRequired";
    } else if (end === null || end < PERIOD_MIN_YEAR || end > currentYear) {
      problems.periodEndYear = "periodEndInvalid";
    } else if (start !== null && end < start) {
      problems.periodEndYear = "periodEndBeforeStart";
    }
  }

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
    periodStartYear: Number(draft.periodStartYear),
    periodEndYear: draft.employmentStatus === "FormerEmployee" ? Number(draft.periodEndYear) : null,
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
    // A row from before the period existed starts empty here, and the form asks.
    periodStartYear: entry.periodStartYear === null ? "" : String(entry.periodStartYear),
    periodEndYear: entry.periodEndYear === null ? "" : String(entry.periodEndYear),
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

/** What a reader sees of the period: "2010 – 2012", "2024 – {ongoing}", or null when the row has
 *  none (written before the period existed), in which case the caller shows the submission month
 *  instead. The ongoing word is the caller's, so this stays free of the message catalogue. */
export function formatSalaryPeriod(
  entry: { periodStartYear: number | null; periodEndYear: number | null },
  ongoingLabel: string,
): string | null {
  if (entry.periodStartYear === null) return null;
  return `${entry.periodStartYear} – ${entry.periodEndYear ?? ongoingLabel}`;
}

/** "Eylül 2026" / "September 2026" for a yyyy-MM. */
export function formatSalaryMonth(submittedMonth: string, locale: string): string {
  const [year, month] = submittedMonth.split("-").map(Number);
  if (!year || !month) return submittedMonth;
  return new Intl.DateTimeFormat(locale, { month: "long", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(year, month - 1, 1)),
  );
}
