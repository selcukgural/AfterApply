import { describe, expect, it } from "vitest";
import {
  EMPTY_REVIEW_DRAFT,
  RATING_KEYS,
  REVIEW_TEXT_MAX_LENGTH,
  buildReviewRequest,
  draftFromReview,
  validateReportDraft,
  validateReviewDraft,
  type ReviewDraft,
} from "./reviewDraft";

const filled: ReviewDraft = {
  employmentStatus: "FormerEmployee",
  title: "  Honest, slow, fair  ",
  pros: "Clear expectations and colleagues who actually review code.",
  cons: "Decisions take a long time and the salary band lags the market.",
  ratings: {
    overallRating: 4,
    managementRating: 3,
    workEnvironmentRating: 5,
    salaryAndBenefitsRating: 2,
    careerAndDevelopmentRating: 4,
  },
};

describe("validateReviewDraft", () => {
  it("accepts a filled-in draft", () => {
    expect(validateReviewDraft(filled)).toEqual({});
  });

  it("names every missing field at once, ratings included", () => {
    const problems = validateReviewDraft(EMPTY_REVIEW_DRAFT);

    expect(problems.employmentStatus).toBe("employmentStatusRequired");
    expect(problems.title).toBe("titleRequired");
    expect(problems.pros).toBe("prosRequired");
    expect(problems.cons).toBe("consRequired");
    for (const key of RATING_KEYS) {
      expect(problems[key]).toBe("ratingRequired");
    }
  });

  it("rejects text too short to say anything and text past the column", () => {
    expect(validateReviewDraft({ ...filled, pros: "Nice." }).pros).toBe("prosTooShort");
    expect(validateReviewDraft({ ...filled, cons: "x".repeat(REVIEW_TEXT_MAX_LENGTH + 1) }).cons).toBe("consTooLong");
    expect(validateReviewDraft({ ...filled, title: "ok" }).title).toBe("titleTooShort");
  });

  it("rejects a rating off the scale", () => {
    expect(validateReviewDraft({ ...filled, ratings: { ...filled.ratings, overallRating: 6 } }).overallRating).toBe(
      "ratingRequired",
    );
  });
});

describe("buildReviewRequest / draftFromReview", () => {
  it("trims the text and flattens the ratings, and round-trips", () => {
    const request = buildReviewRequest(filled);

    expect(request.title).toBe("Honest, slow, fair");
    expect(request.overallRating).toBe(4);
    expect(request.salaryAndBenefitsRating).toBe(2);
    expect(draftFromReview(request)).toEqual({ ...filled, title: "Honest, slow, fair" });
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
