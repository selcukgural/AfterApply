import { describe, expect, it } from "vitest";
import { directoryCountLines } from "./directoryCard";

describe("directoryCountLines", () => {
  it("lists only the kinds the company has, reviews first", () => {
    expect(directoryCountLines({ approvedCount: 3, salaryCount: 2, candidateExperienceCount: 1 })).toEqual([
      { key: "reviewCount", count: 3 },
      { key: "salaryCount", count: 2 },
      { key: "experienceCount", count: 1 },
    ]);
  });

  it("drops the zero lines", () => {
    expect(directoryCountLines({ approvedCount: 0, salaryCount: 1, candidateExperienceCount: 0 })).toEqual([{ key: "salaryCount", count: 1 }]);
    expect(directoryCountLines({ approvedCount: 0, salaryCount: 0, candidateExperienceCount: 2 })).toEqual([{ key: "experienceCount", count: 2 }]);
  });

  it("always keeps at least one line", () => {
    expect(directoryCountLines({ approvedCount: 0, salaryCount: 0, candidateExperienceCount: 0 })).toEqual([{ key: "reviewCount", count: 0 }]);
  });
});
