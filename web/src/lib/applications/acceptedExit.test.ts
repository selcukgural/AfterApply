import { describe, expect, it } from "vitest";
import type { ApplicationStatusHistoryResponse, OccupationRef } from "@/types/api";
import { EMPTY_EXPERIENCE_DRAFT } from "@/lib/candidateExperiences/experienceDraft";
import { EMPTY_SALARY_DRAFT } from "@/lib/companySalaries/salaryDraft";
import {
  acceptedAt,
  exactOccupationMatch,
  exitExperienceDraft,
  exitSalaryDraft,
  exitSections,
  experienceTouched,
  processDurationBetween,
  salaryTouched,
  type ExitSectionInputs,
} from "./acceptedExit";

const open = { hasOwn: false, quotaLeft: 3 };
const base: ExitSectionInputs = {
  experiencesEnabled: true,
  salariesEnabled: true,
  hasCompanyPage: true,
  experienceViewer: open,
  salaryViewer: open,
};

describe("exitSections", () => {
  it("offers both sections when nothing is on record yet", () => {
    expect(exitSections(base)).toEqual({ experience: true, salary: true });
  });

  it("drops a section the person has already filled for this company", () => {
    expect(exitSections({ ...base, experienceViewer: { hasOwn: true, quotaLeft: 3 } })).toEqual({ experience: false, salary: true });
    expect(exitSections({ ...base, salaryViewer: { hasOwn: true, quotaLeft: 3 } })).toEqual({ experience: true, salary: false });
  });

  it("drops a section with no quota left, a feature that is off, or a company without a page", () => {
    expect(exitSections({ ...base, salaryViewer: { hasOwn: false, quotaLeft: 0 } }).salary).toBe(false);
    expect(exitSections({ ...base, experiencesEnabled: false }).experience).toBe(false);
    expect(exitSections({ ...base, salariesEnabled: false }).salary).toBe(false);
    expect(exitSections({ ...base, hasCompanyPage: false })).toEqual({ experience: false, salary: false });
  });

  it("offers nothing while the viewer state is still loading", () => {
    expect(exitSections({ ...base, experienceViewer: undefined, salaryViewer: undefined })).toEqual({ experience: false, salary: false });
  });
});

const move = (toStatus: ApplicationStatusHistoryResponse["toStatus"], changedAt: string) =>
  ({ toStatus, changedAt }) as ApplicationStatusHistoryResponse;

describe("acceptedAt", () => {
  it("takes the latest move to Accepted", () => {
    const history = [
      move("Offer", "2026-09-10T10:00:00Z"),
      move("Accepted", "2026-09-12T10:00:00Z"),
      move("Offer", "2026-09-14T10:00:00Z"),
      move("Accepted", "2026-09-18T10:00:00Z"),
    ];
    expect(acceptedAt(history)).toBe("2026-09-18T10:00:00Z");
  });

  it("is null when the history never reached Accepted", () => {
    expect(acceptedAt([move("Applied", "2026-08-12T10:00:00Z")])).toBeNull();
    expect(acceptedAt([])).toBeNull();
  });
});

describe("processDurationBetween", () => {
  const applied = "2026-08-01T09:00:00Z";
  it.each([
    ["2026-08-05T09:00:00Z", "UnderOneWeek"],
    ["2026-08-08T09:00:00Z", "OneToTwoWeeks"],
    ["2026-08-15T09:00:00Z", "TwoToFourWeeks"],
    ["2026-08-31T09:00:00Z", "TwoToFourWeeks"],
    ["2026-09-18T09:00:00Z", "OneToTwoMonths"],
    ["2026-10-15T09:00:00Z", "OverTwoMonths"],
  ] as const)("accepted %s reads as %s", (accepted, band) => {
    expect(processDurationBetween(applied, accepted)).toBe(band);
  });

  it("says nothing when the acceptance date is unknown or comes before the application", () => {
    expect(processDurationBetween(applied, null)).toBeNull();
    expect(processDurationBetween(applied, "2026-07-01T09:00:00Z")).toBeNull();
    expect(processDurationBetween("not a date", "2026-08-05T09:00:00Z")).toBeNull();
  });
});

describe("exitExperienceDraft", () => {
  it("fills in the outcome and the duration, and leaves the rating to the person", () => {
    const draft = exitExperienceDraft("2026-08-12T09:00:00Z", "2026-09-18T09:00:00Z");
    expect(draft.outcome).toBe("Offer");
    expect(draft.duration).toBe("OneToTwoMonths");
    expect(draft.overall).toBe(0);
    expect(experienceTouched(draft)).toBe(false);
  });

  it("leaves the duration unsaid without an acceptance date", () => {
    expect(exitExperienceDraft("2026-08-12T09:00:00Z", null).duration).toBe("");
  });
});

describe("exitSalaryDraft", () => {
  it("records a current employee since the acceptance year, with the application's arrangement", () => {
    const draft = exitSalaryDraft(" Backend Developer ", "FullTime", "2026-09-18T09:00:00Z", 2026);
    expect(draft).toMatchObject({
      occupationId: null,
      occupationLabel: "Backend Developer",
      employmentType: "FullTime",
      employmentStatus: "CurrentEmployee",
      periodStartYear: "2026",
      periodEndYear: "",
      monthlyNetAmount: "",
      hasBonus: null,
    });
    expect(salaryTouched(draft)).toBe(false);
  });

  it("falls back to this year without an acceptance date", () => {
    expect(exitSalaryDraft("QA", "Contract", null, 2026).periodStartYear).toBe("2026");
  });
});

const occupation = (id: string, nameTr: string, nameEn: string): OccupationRef => ({ id, code: "2512", nameTr, nameEn });

describe("exactOccupationMatch", () => {
  const results = [
    occupation("isco", "Yazılım geliştiricileri", "Software Developers"),
    occupation("backend", "Backend Developer", "Backend Developer"),
    occupation("embedded", "Gömülü Yazılım Geliştirici", "Embedded Software Developer"),
  ];

  it("picks the row whose name is the job title, in either language and any case", () => {
    expect(exactOccupationMatch("backend  developer", results)?.id).toBe("backend");
    expect(exactOccupationMatch("GÖMÜLÜ YAZILIM GELIŞTIRICI", results)).toBeNull(); // dotless/dotted I differ in Turkish
    expect(exactOccupationMatch("Gömülü yazılım geliştirici", results)?.id).toBe("embedded");
    expect(exactOccupationMatch("Software Developers", results)?.id).toBe("isco");
  });

  it("picks nothing for a near match, an empty title or an ambiguous one", () => {
    expect(exactOccupationMatch("Senior Backend Developer", results)).toBeNull();
    expect(exactOccupationMatch("   ", results)).toBeNull();
    expect(exactOccupationMatch("Backend Developer", [...results, occupation("dup", "Backend Developer", "Backend Engineer")])).toBeNull();
  });
});

describe("touched", () => {
  it("counts only what the person entered", () => {
    expect(experienceTouched({ ...EMPTY_EXPERIENCE_DRAFT, overall: 4 })).toBe(true);
    expect(experienceTouched({ ...EMPTY_EXPERIENCE_DRAFT, stages: "Three" })).toBe(true);
    expect(experienceTouched({ ...EMPTY_EXPERIENCE_DRAFT, interviewTypes: ["Video"] })).toBe(true);
    expect(salaryTouched({ ...EMPTY_SALARY_DRAFT, monthlyNetAmount: "85000" })).toBe(true);
    expect(salaryTouched({ ...EMPTY_SALARY_DRAFT, yearsOfExperience: "4" })).toBe(true);
    expect(salaryTouched({ ...EMPTY_SALARY_DRAFT, hasBonus: false })).toBe(true);
  });
});
