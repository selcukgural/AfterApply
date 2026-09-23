import type { Offer, SalaryBasis } from "./compare";

/**
 * The form keeps what the visitor typed, as typed — "90.000", "1,5" — and parses on every render,
 * so a half-typed number never jumps under the cursor. Amounts are whole lira: every character
 * that is not a digit is dropped, which takes both "90.000" and "90,000" as ninety thousand.
 */
export interface OfferDraft {
  name: string;
  basis: SalaryBasis;
  salary: string;
  bonusSalaries: string;
  mealPerDay: string;
  healthPerMonth: string;
  besPerMonth: string;
  remoteDaysPerWeek: string;
  officeDayCost: string;
}

export type DraftField = Exclude<keyof OfferDraft, "basis">;

/** The longest offer name kept — it is a label on a chart, not a place for a letter. */
export const NAME_MAX_LENGTH = 40;

/** Salaries above this are a typo, not an offer; the parse caps rather than overflowing the chart. */
const AMOUNT_MAX = 100_000_000;

export function parseAmount(text: string): number {
  const digits = text.replace(/\D/g, "").slice(0, 12);
  return digits === "" ? 0 : Math.min(Number(digits), AMOUNT_MAX);
}

/**
 * A count that may be fractional ("1,5" salaries of bonus): the one decimal mark is the locale's —
 * a comma in Turkish, a point in English — and the other is taken as a thousands separator.
 */
export function parseCount(text: string, locale: string, max: number): number {
  const decimal = locale === "tr" ? "," : ".";
  const normalised = text
    .split("")
    .filter((char) => /\d/.test(char) || char === decimal)
    .join("")
    .replace(decimal, ".")
    .replace(/[^\d.]/g, "");
  const value = Number.parseFloat(normalised);
  return Number.isFinite(value) ? Math.min(Math.max(value, 0), max) : 0;
}

export function toOffer(draft: OfferDraft, locale: string): Offer {
  return {
    name: draft.name,
    basis: draft.basis,
    salary: parseAmount(draft.salary),
    bonusSalaries: parseCount(draft.bonusSalaries, locale, 12),
    mealPerDay: parseAmount(draft.mealPerDay),
    healthPerMonth: parseAmount(draft.healthPerMonth),
    besPerMonth: parseAmount(draft.besPerMonth),
    remoteDaysPerWeek: Math.round(parseCount(draft.remoteDaysPerWeek, locale, 5)),
    officeDayCost: parseAmount(draft.officeDayCost),
  };
}

/** A whole amount the way the locale writes it — "90.000" in Turkish, "90,000" in English. */
export function formatNumber(value: number, locale: string): string {
  return new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB", { maximumFractionDigits: 0 }).format(
    Math.round(value),
  );
}

/**
 * Lira, no kuruş: "90.000 ₺" / "₺90,000". Written out rather than left to Intl's currency style,
 * whose Turkish output puts the sign in front ("₺90.000") on some ICU builds and behind it on
 * others — the chart and the verdict must not disagree between a server and a browser.
 */
export function formatLira(value: number, locale: string): string {
  const amount = formatNumber(value, locale);
  return locale === "tr" ? `${amount} ₺` : `₺${amount}`;
}

/**
 * The page opens on two filled-in example offers rather than an empty form: the comparison is
 * the point, and an empty form shows none of it. The examples are the design canvas's — a lower
 * gross with benefits and remote days against a higher one with two bonus salaries and a desk.
 */
export function exampleDrafts(locale: string, names: readonly [string, string]): OfferDraft[] {
  return [
    {
      name: names[0],
      basis: "gross",
      salary: formatNumber(90_000, locale),
      bonusSalaries: "0",
      mealPerDay: "330",
      healthPerMonth: formatNumber(1_500, locale),
      besPerMonth: "0",
      remoteDaysPerWeek: "3",
      officeDayCost: "250",
    },
    {
      name: names[1],
      basis: "gross",
      salary: formatNumber(100_000, locale),
      bonusSalaries: "2",
      mealPerDay: "0",
      healthPerMonth: "0",
      besPerMonth: "0",
      remoteDaysPerWeek: "0",
      officeDayCost: "250",
    },
  ];
}

/** A third offer starts empty but for the office cost, which is the visitor's and not the offer's. */
export function emptyDraft(name: string, officeDayCost: string): OfferDraft {
  return {
    name,
    basis: "gross",
    salary: "",
    bonusSalaries: "0",
    mealPerDay: "0",
    healthPerMonth: "0",
    besPerMonth: "0",
    remoteDaysPerWeek: "0",
    officeDayCost,
  };
}
