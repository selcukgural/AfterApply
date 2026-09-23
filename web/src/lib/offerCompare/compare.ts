import { netYear, type PayMonth } from "./netSalary";
import { PAYROLL, WORKDAYS_PER_MONTH, type PayrollYear } from "./payroll";

export type SalaryBasis = "gross" | "net";

/** One job offer as the visitor types it — every amount in TL, monthly unless it says otherwise. */
export interface Offer {
  name: string;
  basis: SalaryBasis;
  /** The monthly salary, gross or net per `basis`. */
  salary: number;
  /** Bonus a year, in monthly salaries ("14 maaş" is 2). */
  bonusSalaries: number;
  /** The meal card's daily face value, VAT included — the way cards are quoted. */
  mealPerDay: number;
  /** Employer-paid private health insurance, per month. */
  healthPerMonth: number;
  /** Employer BES contribution, per month. */
  besPerMonth: number;
  /** 0–5. */
  remoteDaysPerWeek: number;
  /** What an office day costs the visitor — travel, lunch; their own figure. */
  officeDayCost: number;
}

export interface MonthTakeHome {
  /** Everything paid in cash that month, net. */
  net: number;
  /** The part of `net` that is the bonus. */
  bonus: number;
}

export interface OfferResult {
  /** A year of salary, net — bonuses not included. */
  salaryNet: number;
  bonusNet: number;
  /** The meal card's face value over the year. */
  meal: number;
  /** Health insurance + BES over the year, at the employer's cost. */
  perks: number;
  /** Office days × the visitor's cost for one. */
  officeCost: number;
  /** salaryNet + bonusNet + meal + perks − officeCost. */
  total: number;
  officeDaysPerWeek: number;
  /** January first. */
  months: MonthTakeHome[];
}

const MONTHS = 12;

/**
 * The months a bonus is paid in, zero-based. Nobody tells a candidate this, and it moves the
 * cumulative tax, so the page says it: one salary in December, two in June and December, more
 * spread over the quarters' last months.
 */
export function bonusMonths(bonusSalaries: number): number[] {
  if (bonusSalaries <= 0) return [];
  if (bonusSalaries <= 1) return [11];
  if (bonusSalaries <= 2) return [5, 11];
  return [2, 5, 8, 11];
}

/** Which sentence names the bonus months — the message key under `offerCompare.months`. */
export function bonusScheduleKey(bonusSalaries: number): "bonusOne" | "bonusTwo" | "bonusQuarterly" | null {
  const count = bonusMonths(bonusSalaries).length;
  return count === 0 ? null : count === 1 ? "bonusOne" : count === 2 ? "bonusTwo" : "bonusQuarterly";
}

function bonusShares(offer: Offer): number[] {
  const at = bonusMonths(offer.bonusSalaries);
  return Array.from({ length: MONTHS }, (_, month) =>
    at.includes(month) ? (offer.salary * offer.bonusSalaries) / at.length : 0,
  );
}

/**
 * A gross offer's cash, month by month. The benefits count as wage the way the law has it: the
 * meal card only above its exemptions (income tax net of VAT, SGK at face value), health insurance and BES in full for income tax and stamp
 * duty (health insurance then deductible within GVK 63's limits, which BES uses up first), and
 * both in the SGK base only above their joint exemption.
 */
function grossMonths(offer: Offer, withBonus: boolean, year: PayrollYear): number[] {
  const bonus = bonusShares(offer);
  const mealNetOfVat = offer.mealPerDay / (1 + year.mealVatRate);
  const mealTaxable = Math.max(0, mealNetOfVat - year.mealTaxExemptPerDay) * WORKDAYS_PER_MONTH;
  // The SGK side takes the card's face value: VAT is not netted out there (PKF on Law 7577), so a
  // 330 TL card has 30 TL a day in the SGK base while being entirely free of income tax.
  const mealSgk = Math.max(0, offer.mealPerDay - year.mealSgkExemptPerDay) * WORKDAYS_PER_MONTH;
  const perks = offer.healthPerMonth + offer.besPerMonth;
  const perksSgk = Math.max(0, perks - year.sgkBenefitExemptMonthly);

  let deductedSoFar = 0;
  const months: PayMonth[] = bonus.map((share) => {
    const gross = offer.salary + (withBonus ? share : 0);
    const room = Math.max(0, gross * year.insuranceDeductionRate - offer.besPerMonth);
    const insuranceDeduction = Math.max(
      0,
      Math.min(offer.healthPerMonth, room, year.insuranceDeductionAnnualCap - deductedSoFar),
    );
    deductedSoFar += insuranceDeduction;
    return { gross, taxableBenefits: mealTaxable + perks, sgkBenefits: mealSgk + perksSgk, insuranceDeduction };
  });
  return netYear(months, year).map((month) => month.net);
}

/**
 * One offer over a calendar year. A net offer is taken at its word: the same net every month and
 * every bonus salary net too, the employer carrying whatever the brackets add over the year —
 * which is what agreeing a net salary means in Turkey.
 */
export function evaluateOffer(offer: Offer, year: PayrollYear = PAYROLL): OfferResult {
  const bonus = bonusShares(offer);
  let salaryOnly: number[];
  let full: number[];
  if (offer.basis === "net") {
    salaryOnly = bonus.map(() => offer.salary);
    full = bonus.map((share) => offer.salary + share);
  } else {
    salaryOnly = grossMonths(offer, false, year);
    full = grossMonths(offer, true, year);
  }

  const salaryNet = sum(salaryOnly);
  const bonusNet = sum(full) - salaryNet;
  const remote = clamp(offer.remoteDaysPerWeek, 0, 5);
  const officeDaysPerWeek = 5 - remote;
  const meal = offer.mealPerDay * WORKDAYS_PER_MONTH * MONTHS;
  const perks = (offer.healthPerMonth + offer.besPerMonth) * MONTHS;
  const officeCost = (officeDaysPerWeek / 5) * WORKDAYS_PER_MONTH * MONTHS * offer.officeDayCost;

  return {
    salaryNet,
    bonusNet,
    meal,
    perks,
    officeCost,
    total: salaryNet + bonusNet + meal + perks - officeCost,
    officeDaysPerWeek,
    months: full.map((net, month) => {
      // A bonus month's cash splits into the month's salary (as it would have netted without the
      // bonus) and the rest; the brackets a bonus pushes the later months into stay in their salary.
      const bonusPart = bonus[month] > 0 ? Math.max(0, net - salaryOnly[month]) : 0;
      return { net, bonus: bonusPart };
    }),
  };
}

export interface Comparison {
  results: OfferResult[];
  /** The offer that leaves the most a year; null until two offers have a salary, or on a tie. */
  best: number | null;
  /** Two or more offers with a salary, the top two less than a lira a year apart. */
  tie: boolean;
  /** How much more the best leaves than the runner-up, a year. */
  lead: number;
  /** The biggest single month across every offer — the month chart's shared scale. */
  maxMonth: number;
}

/** Less than a lira a year apart is a tie; the page says so rather than crowning one. */
const TIE = 1;

export function compareOffers(offers: readonly Offer[], year: PayrollYear = PAYROLL): Comparison {
  const results = offers.map((offer) => evaluateOffer(offer, year));
  const ranked = results
    .map((result, index) => ({ index, total: result.total, valid: offers[index].salary > 0 }))
    .filter((entry) => entry.valid)
    .sort((a, b) => b.total - a.total);

  const maxMonth = Math.max(0, ...results.flatMap((result) => result.months.map((month) => month.net)));
  if (ranked.length < 2) return { results, best: null, tie: false, lead: 0, maxMonth };

  const lead = ranked[0].total - ranked[1].total;
  if (lead < TIE) return { results, best: null, tie: true, lead: 0, maxMonth };
  return { results, best: ranked[0].index, tie: false, lead, maxMonth };
}

function sum(values: readonly number[]): number {
  return values.reduce((total, value) => total + value, 0);
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
