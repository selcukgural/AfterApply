import { describe, expect, it } from "vitest";
import type { MyCompanySalary } from "@/types/api";
import {
  EMPTY_SALARY_DRAFT,
  bandForYears,
  buildSalaryRequest,
  draftFromSalary,
  formatAmount,
  formatSalaryMonth,
  occupationName,
  occupationOtherName,
  parseAmount,
  validateSalaryDraft,
  type SalaryDraft,
} from "./salaryDraft";

const BACKEND = "11111111-1111-4111-8111-111111111111";

const filled: SalaryDraft = {
  occupationId: BACKEND,
  occupationLabel: "Backend Developer",
  yearsOfExperience: "6",
  employmentType: "FullTime",
  employmentStatus: "CurrentEmployee",
  monthlyNetAmount: "95.000",
  currency: "TRY",
  hasBonus: true,
  annualBonusAmount: "120000",
};

describe("parseAmount", () => {
  it.each([
    ["95000", 95000],
    ["95.000", 95000],
    ["95,000", 95000],
    ["95 000", 95000],
    ["1.250.000", 1250000],
    ["45000,50", 45000.5],
    ["45000.50", 45000.5],
    ["45.000,50", 45000.5],
    ["1.250.000,75", 1250000.75],
    ["4000.5", 4000.5],
  ])("reads %s as %d", (raw, expected) => {
    expect(parseAmount(raw)).toBe(expected);
  });

  it.each(["", "  ", "abc", "95k", "-1", "1.2.3,4.5", ".", "45.000.50", "12.34.56", "1,2,3"])("rejects %j", (raw) => {
    expect(parseAmount(raw)).toBeNull();
  });
});

describe("validateSalaryDraft", () => {
  it("accepts a filled-in draft", () => {
    expect(validateSalaryDraft(filled)).toEqual({});
  });

  it("accepts no bonus without an amount", () => {
    expect(validateSalaryDraft({ ...filled, hasBonus: false, annualBonusAmount: "" })).toEqual({});
  });

  it("names every missing field at once", () => {
    expect(validateSalaryDraft(EMPTY_SALARY_DRAFT)).toEqual({
      occupation: "occupationRequired",
      yearsOfExperience: "yearsInvalid",
      employmentType: "employmentTypeRequired",
      employmentStatus: "employmentStatusRequired",
      monthlyNetAmount: "amountInvalid",
      hasBonus: "bonusAnswerRequired",
    });
  });

  it("wants a picked occupation, not typed text", () => {
    // The label alone is what a person typed after a pick cleared the id — not an occupation.
    expect(validateSalaryDraft({ ...filled, occupationId: null, occupationLabel: "Backend Dev" })).toEqual({
      occupation: "occupationRequired",
    });
  });

  it.each(["-1", "51", "2.5", "abc"])("refuses %s years", (yearsOfExperience) => {
    expect(validateSalaryDraft({ ...filled, yearsOfExperience })).toEqual({ yearsOfExperience: "yearsInvalid" });
  });

  it("refuses an amount off the scale", () => {
    expect(validateSalaryDraft({ ...filled, monthlyNetAmount: "0" })).toEqual({ monthlyNetAmount: "amountInvalid" });
    expect(validateSalaryDraft({ ...filled, monthlyNetAmount: "10000001" })).toEqual({ monthlyNetAmount: "amountInvalid" });
  });

  it("wants a bonus amount once the answer is yes", () => {
    expect(validateSalaryDraft({ ...filled, annualBonusAmount: "" })).toEqual({ annualBonusAmount: "bonusAmountInvalid" });
  });
});

describe("buildSalaryRequest", () => {
  it("parses the strings and drops the bonus amount when the answer is no", () => {
    expect(buildSalaryRequest(filled)).toEqual({
      occupationId: BACKEND,
      yearsOfExperience: 6,
      employmentType: "FullTime",
      employmentStatus: "CurrentEmployee",
      monthlyNetAmount: 95000,
      currency: "TRY",
      hasBonus: true,
      annualBonusAmount: 120000,
    });
    expect(buildSalaryRequest({ ...filled, hasBonus: false, annualBonusAmount: "999" })).toMatchObject({
      hasBonus: false,
      annualBonusAmount: null,
    });
  });
});

describe("draftFromSalary", () => {
  const entry: MyCompanySalary = {
    id: "e1",
    companyId: "c1",
    companySlug: "beta-a-s",
    companyName: "Beta A.Ş.",
    occupation: { id: BACKEND, code: "EK-0001", nameTr: "Backend Geliştirici", nameEn: "Backend Developer" },
    yearsOfExperience: 9,
    employmentType: "Contract",
    employmentStatus: "FormerEmployee",
    monthlyNetAmount: 130000,
    currency: "EUR",
    annualBonusAmount: null,
    submittedAt: "2026-09-16T10:00:00Z",
    updatedAt: "2026-09-16T10:00:00Z",
  };

  it("round-trips a row into a valid draft, labelled in the reader's language", () => {
    const draft = draftFromSalary(entry, "tr");
    expect(draft).toEqual({
      occupationId: BACKEND,
      occupationLabel: "Backend Geliştirici",
      yearsOfExperience: "9",
      employmentType: "Contract",
      employmentStatus: "FormerEmployee",
      monthlyNetAmount: "130000",
      currency: "EUR",
      hasBonus: false,
      annualBonusAmount: "",
    });
    expect(validateSalaryDraft(draft)).toEqual({});
    expect(draftFromSalary({ ...entry, annualBonusAmount: 5000 }, "en")).toMatchObject({
      occupationLabel: "Backend Developer",
      hasBonus: true,
      annualBonusAmount: "5000",
    });
  });
});

describe("occupationName", () => {
  const ref = { id: BACKEND, code: "2512", nameTr: "Yazılım geliştiricileri", nameEn: "Software Developers" };

  it("picks the reader's language and offers the other underneath", () => {
    expect(occupationName(ref, "tr")).toBe("Yazılım geliştiricileri");
    expect(occupationName(ref, "en")).toBe("Software Developers");
    expect(occupationName(ref, "de")).toBe("Software Developers");
    expect(occupationOtherName(ref, "tr")).toBe("Software Developers");
    expect(occupationOtherName(ref, "en")).toBe("Yazılım geliştiricileri");
  });
});

describe("bandForYears", () => {
  it.each([
    [0, "ZeroToOne"],
    [1, "ZeroToOne"],
    [2, "TwoToFour"],
    [4, "TwoToFour"],
    [5, "FiveToNine"],
    [9, "FiveToNine"],
    [10, "TenPlus"],
    [50, "TenPlus"],
  ])("%d years → %s", (years, band) => {
    expect(bandForYears(years)).toBe(band);
  });
});

describe("formatting", () => {
  it("formats a whole amount in the reader's locale", () => {
    expect(formatAmount("tr", 95000, "TRY").replace(/ /g, " ")).toBe("₺95.000");
    expect(formatAmount("en", 95000, "TRY").replace(/ /g, " ")).toBe("TRY 95,000");
    expect(formatAmount("en", 4000, "EUR")).toBe("€4,000");
  });

  it("formats a month at month precision", () => {
    expect(formatSalaryMonth("2026-09", "en")).toBe("September 2026");
    expect(formatSalaryMonth("2026-09", "tr")).toBe("Eylül 2026");
    expect(formatSalaryMonth("garbage", "en")).toBe("garbage");
  });
});
