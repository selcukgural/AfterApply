import type { CompanyReviewRequest, EmploymentStatus, ReviewReportReason } from "@/types/api";

// Mirrors CompanyReview's constants and ReviewContentRules on the server. The server is the one
// that decides; these exist so the form can say the same thing before a round trip.
export const REVIEW_TITLE_MIN_LENGTH = 3;
export const REVIEW_TITLE_MAX_LENGTH = 120;
export const REVIEW_TEXT_MIN_LENGTH = 20;
export const REVIEW_TEXT_MAX_LENGTH = 2000;
export const REPORT_NOTE_MAX_LENGTH = 500;
export const RATING_MIN = 1;
export const RATING_MAX = 5;

export const EMPLOYMENT_STATUSES: readonly EmploymentStatus[] = ["CurrentEmployee", "FormerEmployee", "Intern"];

export const REPORT_REASONS: readonly ReviewReportReason[] = [
  "Insult",
  "Profanity",
  "PersonalInformation",
  "MisleadingInformation",
  "Advertising",
  "Spam",
  "Other",
];

/** The five ratings, in the order the form and the summary panel show them. Keys are the request
 *  field names, so the same list drives both. */
export const RATING_KEYS = [
  "overallRating",
  "managementRating",
  "workEnvironmentRating",
  "salaryAndBenefitsRating",
  "careerAndDevelopmentRating",
] as const;

export type RatingKey = (typeof RATING_KEYS)[number];

export interface ReviewDraft {
  employmentStatus: EmploymentStatus | "";
  title: string;
  pros: string;
  cons: string;
  ratings: Record<RatingKey, number>;
}

export const EMPTY_REVIEW_DRAFT: ReviewDraft = {
  employmentStatus: "",
  title: "",
  pros: "",
  cons: "",
  ratings: {
    overallRating: 0,
    managementRating: 0,
    workEnvironmentRating: 0,
    salaryAndBenefitsRating: 0,
    careerAndDevelopmentRating: 0,
  },
};

/** Translation keys under `companyReviews.form.problems`. */
export type ReviewDraftProblem =
  | "employmentStatusRequired"
  | "titleRequired"
  | "titleTooShort"
  | "titleTooLong"
  | "prosRequired"
  | "prosTooShort"
  | "prosTooLong"
  | "consRequired"
  | "consTooShort"
  | "consTooLong"
  | "ratingRequired";

export type ReviewDraftField = "employmentStatus" | "title" | "pros" | "cons" | RatingKey;

/** Every problem at once, keyed by field, so the form can mark each field rather than the first. */
export function validateReviewDraft(draft: ReviewDraft): Partial<Record<ReviewDraftField, ReviewDraftProblem>> {
  const problems: Partial<Record<ReviewDraftField, ReviewDraftProblem>> = {};

  if (draft.employmentStatus === "") {
    problems.employmentStatus = "employmentStatusRequired";
  }

  const title = draft.title.trim();
  if (title.length === 0) problems.title = "titleRequired";
  else if (title.length < REVIEW_TITLE_MIN_LENGTH) problems.title = "titleTooShort";
  else if (title.length > REVIEW_TITLE_MAX_LENGTH) problems.title = "titleTooLong";

  const pros = draft.pros.trim();
  if (pros.length === 0) problems.pros = "prosRequired";
  else if (pros.length < REVIEW_TEXT_MIN_LENGTH) problems.pros = "prosTooShort";
  else if (pros.length > REVIEW_TEXT_MAX_LENGTH) problems.pros = "prosTooLong";

  const cons = draft.cons.trim();
  if (cons.length === 0) problems.cons = "consRequired";
  else if (cons.length < REVIEW_TEXT_MIN_LENGTH) problems.cons = "consTooShort";
  else if (cons.length > REVIEW_TEXT_MAX_LENGTH) problems.cons = "consTooLong";

  for (const key of RATING_KEYS) {
    const value = draft.ratings[key];
    if (!Number.isInteger(value) || value < RATING_MIN || value > RATING_MAX) {
      problems[key] = "ratingRequired";
    }
  }

  return problems;
}

/** Only call after validateReviewDraft returned no problems — the cast on employmentStatus relies on it. */
export function buildReviewRequest(draft: ReviewDraft): CompanyReviewRequest {
  return {
    employmentStatus: draft.employmentStatus as EmploymentStatus,
    title: draft.title.trim(),
    pros: draft.pros.trim(),
    cons: draft.cons.trim(),
    ...draft.ratings,
  };
}

/** The draft an edit form starts from. */
export function draftFromReview(review: CompanyReviewRequest): ReviewDraft {
  return {
    employmentStatus: review.employmentStatus,
    title: review.title,
    pros: review.pros,
    cons: review.cons,
    ratings: {
      overallRating: review.overallRating,
      managementRating: review.managementRating,
      workEnvironmentRating: review.workEnvironmentRating,
      salaryAndBenefitsRating: review.salaryAndBenefitsRating,
      careerAndDevelopmentRating: review.careerAndDevelopmentRating,
    },
  };
}

export type ReportDraftProblem = "noteRequiredForOther" | "noteTooLong";

/** "Other" with no explanation gives the admin nothing to act on — same rule as the server's. */
export function validateReportDraft(reason: ReviewReportReason, note: string): ReportDraftProblem | null {
  const trimmed = note.trim();
  if (reason === "Other" && trimmed.length === 0) return "noteRequiredForOther";
  if (trimmed.length > REPORT_NOTE_MAX_LENGTH) return "noteTooLong";
  return null;
}
