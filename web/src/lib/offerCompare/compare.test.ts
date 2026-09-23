import { describe, expect, it } from "vitest";
import { bonusMonths, bonusScheduleKey, compareOffers, evaluateOffer, type Offer } from "./compare";

const base: Offer = {
  name: "A",
  basis: "gross",
  salary: 90_000,
  bonusSalaries: 0,
  mealPerDay: 0,
  healthPerMonth: 0,
  besPerMonth: 0,
  remoteDaysPerWeek: 5,
  officeDayCost: 0,
};

describe("bonusMonths", () => {
  it("pays one in December, two in June and December, more by quarter", () => {
    expect(bonusMonths(0)).toEqual([]);
    expect(bonusMonths(1)).toEqual([11]);
    expect(bonusMonths(2)).toEqual([5, 11]);
    expect(bonusMonths(4)).toEqual([2, 5, 8, 11]);
  });
});

describe("bonusScheduleKey", () => {
  it("names the sentence that matches the months", () => {
    expect(bonusScheduleKey(0)).toBeNull();
    expect(bonusScheduleKey(0.5)).toBe("bonusOne");
    expect(bonusScheduleKey(2)).toBe("bonusTwo");
    expect(bonusScheduleKey(3)).toBe("bonusQuarterly");
  });
});

describe("evaluateOffer", () => {
  it("sums a plain gross salary's twelve nets", () => {
    const result = evaluateOffer(base);
    expect(Math.round(result.salaryNet)).toBe(760_332);
    expect(result.bonusNet).toBe(0);
    expect(Math.round(result.months[0].net)).toBe(68_804);
    expect(Math.round(result.months[11].net)).toBe(61_028);
  });

  it("takes a net offer at its word: the same net every month, bonuses net too", () => {
    const result = evaluateOffer({ ...base, basis: "net", salary: 65_000, bonusSalaries: 1 });
    expect(result.salaryNet).toBe(65_000 * 12);
    expect(result.bonusNet).toBe(65_000);
    expect(result.months[0]).toEqual({ net: 65_000, bonus: 0 });
    expect(result.months[11]).toEqual({ net: 130_000, bonus: 65_000 });
  });

  it("splits a gross bonus month into its salary and the bonus", () => {
    const result = evaluateOffer({ ...base, salary: 100_000, bonusSalaries: 2 });
    expect(Math.round(result.months[5].net)).toBe(127_044);
    expect(result.months[5].bonus).toBeGreaterThan(60_000);
    expect(result.months[4].bonus).toBe(0);
    expect(Math.round(result.salaryNet + result.bonusNet)).toBe(956_464);
  });

  it("adds benefits at face value and takes office days off at the visitor's cost", () => {
    const result = evaluateOffer({ ...base, mealPerDay: 330, healthPerMonth: 1_500, remoteDaysPerWeek: 3, officeDayCost: 250 });
    expect(result.meal).toBe(330 * 22 * 12);
    expect(result.perks).toBe(1_500 * 12);
    expect(result.officeDaysPerWeek).toBe(2);
    expect(result.officeCost).toBeCloseTo((2 / 5) * 22 * 12 * 250, 6);
    expect(result.total).toBeCloseTo(result.salaryNet + result.meal + result.perks - result.officeCost, 6);
  });

  it("keeps a 330 card out of income tax but puts its 30 TL a day over 300 into the SGK base", () => {
    const plain = evaluateOffer(base).salaryNet;
    const withCard = evaluateOffer({ ...base, mealPerDay: 330 }).salaryNet;
    // 30 × 22 = 660 a month in the SGK base → 99 TL of employee share, less the income tax that
    // share saves; nothing else moves.
    const monthlyCost = (plain - withCard) / 12;
    expect(monthlyCost).toBeGreaterThan(60);
    expect(monthlyCost).toBeLessThan(99.01);
  });

  it("takes nothing for a card at or under 300 a day", () => {
    expect(evaluateOffer({ ...base, mealPerDay: 300 }).salaryNet).toBeCloseTo(evaluateOffer(base).salaryNet, 6);
  });

  it("taxes a meal card above the exemption, out of the cash", () => {
    const rich = evaluateOffer({ ...base, mealPerDay: 550 });
    expect(rich.salaryNet).toBeLessThan(evaluateOffer(base).salaryNet);
  });

  it("costs a little stamp duty for employer health insurance within the deduction, more for BES", () => {
    const plain = evaluateOffer(base).salaryNet;
    const health = evaluateOffer({ ...base, healthPerMonth: 1_500 }).salaryNet;
    const bes = evaluateOffer({ ...base, besPerMonth: 1_500 }).salaryNet;
    expect(plain - health).toBeCloseTo(1_500 * 0.00759 * 12, 0);
    expect(plain - bes).toBeGreaterThan(plain - health);
  });
});

describe("compareOffers", () => {
  const a: Offer = { ...base, mealPerDay: 330, healthPerMonth: 1_500, remoteDaysPerWeek: 3, officeDayCost: 250 };
  const b: Offer = { ...base, name: "B", salary: 100_000, bonusSalaries: 2, remoteDaysPerWeek: 0, officeDayCost: 250 };

  it("names the offer that leaves more and by how much (the canvas example)", () => {
    const comparison = compareOffers([a, b]);
    expect(comparison.best).toBe(1);
    expect(comparison.tie).toBe(false);
    // 891.464 − 839.452 once the health insurance's stamp duty is counted.
    expect(comparison.lead).toBeGreaterThan(51_000);
    expect(comparison.lead).toBeLessThan(53_000);
    expect(comparison.maxMonth).toBeCloseTo(comparison.results[1].months[11].net, 6);
  });

  it("calls a tie a tie", () => {
    const comparison = compareOffers([a, { ...a, name: "A2" }]);
    expect(comparison.best).toBeNull();
    expect(comparison.tie).toBe(true);
  });

  it("waits for two salaries before naming anyone", () => {
    const comparison = compareOffers([a, { ...b, salary: 0 }]);
    expect(comparison.best).toBeNull();
    expect(comparison.tie).toBe(false);
  });
});
