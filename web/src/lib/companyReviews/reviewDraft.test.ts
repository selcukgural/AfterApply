import { describe, expect, it } from "vitest";
import type { MyCompanyReview } from "@/types/api";
import {
  EMPTY_REVIEW_DRAFT,
  buildReviewRequest,
  draftFromReview,
  isCapReached,
  suggestionsFor,
  togglePick,
  validateReportDraft,
  validateReviewDraft,
  type ReviewDraft,
} from "./reviewDraft";
import { MAX_PICKS_PER_KIND, statementsFor } from "./statementCatalogue";

const filled: ReviewDraft = {
  employmentStatus: "FormerEmployee",
  overall: 4,
  ratings: { WorkEnvironment: 5, Pay: 2 },
  liked: ["environment.pos.team_communication"],
  improvable: ["pay.imp.salary_level"],
};

const likedKeys = statementsFor("WorkEnvironment", "Liked").map((s) => s.key);

describe("validateReviewDraft", () => {
  it("accepts a filled-in draft", () => {
    expect(validateReviewDraft(filled)).toEqual({});
  });

  it("accepts the bare minimum: a relationship and an overall rating", () => {
    expect(validateReviewDraft({ ...EMPTY_REVIEW_DRAFT, employmentStatus: "Intern", overall: 3 })).toEqual({});
  });

  it("names the two required fields at once and nothing else", () => {
    expect(validateReviewDraft(EMPTY_REVIEW_DRAFT)).toEqual({
      employmentStatus: "employmentStatusRequired",
      overall: "overallRequired",
    });
  });

  it("treats a blank optional category as fine and an off-scale one as a problem", () => {
    expect(validateReviewDraft({ ...filled, ratings: { Pay: 0 } })).toEqual({});
    expect(validateReviewDraft({ ...filled, ratings: { Pay: 6 } }).Pay).toBe("ratingInvalid");
    expect(validateReviewDraft({ ...filled, overall: 6 }).overall).toBe("overallRequired");
  });

  it("caps each list at five and refuses keys it does not know or from the other list", () => {
    expect(validateReviewDraft({ ...filled, liked: likedKeys.slice(0, 6) }).liked).toBe("tooManyLiked");
    expect(validateReviewDraft({ ...filled, liked: likedKeys.slice(0, 5) })).toEqual({});
    expect(validateReviewDraft({ ...filled, improvable: ["pay.imp.made_up"] }).improvable).toBe("unknownStatement");
    expect(validateReviewDraft({ ...filled, improvable: ["environment.pos.team_communication"] }).improvable).toBe(
      "unknownStatement",
    );
  });
});

describe("buildReviewRequest / draftFromReview", () => {
  it("sends only the rated categories and round-trips through the author's view", () => {
    const request = buildReviewRequest({ ...filled, ratings: { ...filled.ratings, Onboarding: 0 } });

    expect(request).toEqual({
      employmentStatus: "FormerEmployee",
      overallRating: 4,
      categoryRatings: [
        { category: "WorkEnvironment", rating: 5 },
        { category: "Pay", rating: 2 },
      ],
      likedStatements: ["environment.pos.team_communication"],
      improvableStatements: ["pay.imp.salary_level"],
    });

    const mine: MyCompanyReview = {
      id: "r1",
      companyId: "c1",
      companySlug: "acme",
      companyName: "Acme",
      format: "Structured",
      employmentStatus: request.employmentStatus,
      overallRating: request.overallRating,
      categoryRatings: request.categoryRatings,
      legacySalaryAndBenefitsRating: null,
      likedStatements: request.likedStatements,
      improvableStatements: request.improvableStatements,
      title: null,
      pros: null,
      cons: null,
      status: "Approved",
      rejectionReason: null,
      submittedAt: "2026-09-16T10:00:00Z",
      moderatedAt: null,
    };
    expect(draftFromReview(mine)).toEqual(filled);
  });

  it("starts a legacy review's edit from its mapped ratings and no picks", () => {
    const legacy: MyCompanyReview = {
      id: "r2",
      companyId: "c1",
      companySlug: "acme",
      companyName: "Acme",
      format: "Legacy",
      employmentStatus: "CurrentEmployee",
      overallRating: 5,
      categoryRatings: [
        { category: "WorkEnvironment", rating: 4 },
        { category: "Management", rating: 3 },
        { category: "CareerGrowth", rating: 5 },
      ],
      legacySalaryAndBenefitsRating: 2,
      likedStatements: [],
      improvableStatements: [],
      title: "Old title",
      pros: "Old pros text that nobody sees any more.",
      cons: "Old cons text that nobody sees any more.",
      status: "Approved",
      rejectionReason: null,
      submittedAt: "2026-09-13T10:00:00Z",
      moderatedAt: "2026-09-13T11:00:00Z",
    };

    expect(draftFromReview(legacy)).toEqual({
      employmentStatus: "CurrentEmployee",
      overall: 5,
      ratings: { WorkEnvironment: 4, Management: 3, CareerGrowth: 5 },
      liked: [],
      improvable: [],
    });
  });
});

describe("suggestionsFor", () => {
  it("offers nothing until there is a rating, then three of the matching kind", () => {
    expect(suggestionsFor("Pay", 0)).toEqual([]);
    expect(suggestionsFor("Pay", 4).map((s) => s.kind)).toEqual(["Liked", "Liked", "Liked"]);
    expect(suggestionsFor("Pay", 5).map((s) => s.key)).toEqual(statementsFor("Pay", "Liked").slice(0, 3).map((s) => s.key));
    expect(suggestionsFor("Pay", 3).map((s) => s.kind)).toEqual(["Improve", "Improve", "Improve"]);
    expect(suggestionsFor("Pay", 1).map((s) => s.kind)).toEqual(["Improve", "Improve", "Improve"]);
  });

  it("never offers more than the category has", () => {
    expect(suggestionsFor("Onboarding", 5)).toHaveLength(3);
    expect(suggestionsFor("Overall", 2)).toHaveLength(3);
  });
});

describe("togglePick", () => {
  it("adds to the list its kind belongs to and removes on a second toggle", () => {
    const added = togglePick(filled, "pay.pos.salary_level");
    expect(added.liked).toEqual(["environment.pos.team_communication", "pay.pos.salary_level"]);
    expect(added.improvable).toEqual(filled.improvable);

    expect(togglePick(added, "pay.pos.salary_level")).toEqual(filled);
  });

  it("keeps the draft untouched at the cap and for an unknown key", () => {
    const full = { ...filled, liked: likedKeys.slice(0, MAX_PICKS_PER_KIND) };
    expect(isCapReached(full, "Liked")).toBe(true);
    expect(isCapReached(full, "Improve")).toBe(false);
    expect(togglePick(full, "pay.pos.salary_level")).toBe(full);
    expect(togglePick(filled, "nope.pos.nothing")).toBe(filled);
    // Removing is always allowed, even at the cap.
    expect(togglePick(full, likedKeys[0]).liked).toHaveLength(MAX_PICKS_PER_KIND - 1);
  });
});

describe("validateReportDraft", () => {
  it("requires a note only for Other", () => {
    expect(validateReportDraft("Other", "   ")).toBe("noteRequiredForOther");
    expect(validateReportDraft("Other", "Quotes a private message.")).toBeNull();
    expect(validateReportDraft("Spam", "")).toBeNull();
  });

  it("bounds the note", () => {
    expect(validateReportDraft("Spam", "x".repeat(501))).toBe("noteTooLong");
  });
});
