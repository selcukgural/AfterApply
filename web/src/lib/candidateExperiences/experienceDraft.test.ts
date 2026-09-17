import { describe, expect, it } from "vitest";
import type { MyCandidateExperience } from "@/types/api";
import { INTERVIEW_TYPES } from "./statementCatalogue";
import {
  EMPTY_EXPERIENCE_DRAFT,
  buildExperienceRequest,
  draftFromExperience,
  formatQuarter,
  isCapReached,
  suggestionsFor,
  toggleInterviewType,
  togglePick,
  validateExperienceDraft,
} from "./experienceDraft";

describe("experience draft", () => {
  it("needs only the overall rating", () => {
    expect(validateExperienceDraft(EMPTY_EXPERIENCE_DRAFT)).toEqual({ overall: "overallRequired" });
    expect(validateExperienceDraft({ ...EMPTY_EXPERIENCE_DRAFT, overall: 3 })).toEqual({});
  });

  it("flags an off-scale category and a bad pick list", () => {
    const problems = validateExperienceDraft({
      ...EMPTY_EXPERIENCE_DRAFT,
      overall: 4,
      ratings: { Punctuality: 9 },
      liked: ["outcome.imp.notification"],
      improvable: ["a", "b", "c", "d", "e", "f"],
    });
    expect(problems.Punctuality).toBe("ratingInvalid");
    expect(problems.liked).toBe("unknownStatement");
    expect(problems.improvable).toBe("unknownStatement");
  });

  it("builds a request with nulls for facts left unsaid and only rated categories", () => {
    const request = buildExperienceRequest({
      ...EMPTY_EXPERIENCE_DRAFT,
      overall: 4,
      ratings: { Communication: 5, Punctuality: 0 },
      liked: ["communication.pos.timely_information"],
      outcome: "NoResponse",
      interviewTypes: ["Video"],
    });
    expect(request).toEqual({
      overallRating: 4,
      categoryRatings: [{ category: "Communication", rating: 5 }],
      likedStatements: ["communication.pos.timely_information"],
      improvableStatements: [],
      outcome: "NoResponse",
      duration: null,
      stages: null,
      interviewTypes: ["Video"],
    });
  });

  it("round-trips an entry through the edit draft", () => {
    const entry: MyCandidateExperience = {
      id: "e1",
      companyId: "c1",
      companySlug: "beta-as",
      companyName: "Beta A.Ş.",
      overallRating: 2,
      categoryRatings: [{ category: "OutcomeCommunication", rating: 1 }],
      likedStatements: [],
      improvableStatements: ["outcome.imp.notification"],
      outcome: "NoResponse",
      duration: "OneToTwoMonths",
      stages: null,
      interviewTypes: ["Video", "TakeHomeAssignment"],
      submittedAt: "2026-09-17T10:00:00Z",
      updatedAt: "2026-09-17T10:00:00Z",
    };
    const draft = draftFromExperience(entry);
    expect(draft.stages).toBe("");
    expect(draft.ratings).toEqual({ OutcomeCommunication: 1 });
    expect(buildExperienceRequest(draft)).toEqual({
      overallRating: 2,
      categoryRatings: [{ category: "OutcomeCommunication", rating: 1 }],
      likedStatements: [],
      improvableStatements: ["outcome.imp.notification"],
      outcome: "NoResponse",
      duration: "OneToTwoMonths",
      stages: null,
      interviewTypes: ["Video", "TakeHomeAssignment"],
    });
  });

  it("toggles picks within the cap and refuses unknown keys", () => {
    let draft = { ...EMPTY_EXPERIENCE_DRAFT, overall: 5 };
    draft = togglePick(draft, "general.pos.clear_process");
    expect(draft.liked).toEqual(["general.pos.clear_process"]);
    expect(togglePick(draft, "nope.pos.x")).toBe(draft);
    for (const key of ["respectful_approach", "positive_process", "professional_process", "would_apply_again"]) {
      draft = togglePick(draft, `general.pos.${key}`);
    }
    expect(isCapReached(draft, "Liked")).toBe(true);
    expect(togglePick(draft, "communication.pos.timely_information")).toBe(draft);
    expect(togglePick(draft, "general.pos.clear_process").liked).toHaveLength(4);
  });

  it("suggests liked statements for a high rating and improvable ones for a low one", () => {
    expect(suggestionsFor("Punctuality", 5).every((s) => s.kind === "Liked")).toBe(true);
    expect(suggestionsFor("Punctuality", 2).every((s) => s.kind === "Improve")).toBe(true);
    expect(suggestionsFor("Punctuality", 0)).toEqual([]);
  });

  it("keeps interview types in catalogue order when toggled", () => {
    let draft = toggleInterviewType(EMPTY_EXPERIENCE_DRAFT, "Panel", INTERVIEW_TYPES);
    draft = toggleInterviewType(draft, "Phone", INTERVIEW_TYPES);
    expect(draft.interviewTypes).toEqual(["Phone", "Panel"]);
    expect(toggleInterviewType(draft, "Panel", INTERVIEW_TYPES).interviewTypes).toEqual(["Phone"]);
  });

  it("formats the quarter per locale and leaves junk alone", () => {
    expect(formatQuarter("2026-Q3", "tr")).toBe("2026 Ç3");
    expect(formatQuarter("2026-Q3", "en")).toBe("2026 Q3");
    expect(formatQuarter("weird", "tr")).toBe("weird");
  });
});
