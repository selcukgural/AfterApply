import type {
  CompanyReviewRequest,
  EmploymentStatus,
  MyCompanyReview,
  ReviewCategory,
  ReviewReportReason,
  ReviewStatementKind,
} from "@/types/api";
import {
  LEGACY_CATEGORY_MAP,
  MAX_PICKS_PER_KIND,
  OPTIONAL_CATEGORIES,
  SUGGESTION_COUNT,
  findStatement,
  statementsFor,
  type ReviewStatement,
} from "@/lib/companyReviews/statementCatalogue";

// Mirrors CompanyReview's constants and StructuredReviewRules on the server. The server is the
// one that decides; these exist so the form can say the same thing before a round trip.
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

/** What the form holds. `overall` is the one required rating; `ratings` holds only the optional
 *  categories the author has rated (0 or absent = left blank, which is different from any number);
 *  the two pick lists are catalogue keys in the order they were picked. */
export interface ReviewDraft {
  employmentStatus: EmploymentStatus | "";
  overall: number;
  ratings: Partial<Record<ReviewCategory, number>>;
  liked: string[];
  improvable: string[];
}

export const EMPTY_REVIEW_DRAFT: ReviewDraft = {
  employmentStatus: "",
  overall: 0,
  ratings: {},
  liked: [],
  improvable: [],
};

/** Translation keys under `companyReviews.form.problems`. */
export type ReviewDraftProblem =
  | "employmentStatusRequired"
  | "overallRequired"
  | "ratingInvalid"
  | "tooManyLiked"
  | "tooManyImprovable"
  | "unknownStatement";

export type ReviewDraftField = "employmentStatus" | "overall" | "liked" | "improvable" | ReviewCategory;

function isOnScale(value: number | undefined): value is number {
  return Number.isInteger(value) && (value as number) >= RATING_MIN && (value as number) <= RATING_MAX;
}

/** Every problem at once, keyed by field, so the form can mark each field rather than the first. */
export function validateReviewDraft(draft: ReviewDraft): Partial<Record<ReviewDraftField, ReviewDraftProblem>> {
  const problems: Partial<Record<ReviewDraftField, ReviewDraftProblem>> = {};

  if (draft.employmentStatus === "") {
    problems.employmentStatus = "employmentStatusRequired";
  }

  if (!isOnScale(draft.overall)) {
    problems.overall = "overallRequired";
  }

  for (const category of OPTIONAL_CATEGORIES) {
    const value = draft.ratings[category];
    // Absent or zero is "left blank" and fine; anything else must be on the scale.
    if (value !== undefined && value !== 0 && !isOnScale(value)) {
      problems[category] = "ratingInvalid";
    }
  }

  if (draft.liked.length > MAX_PICKS_PER_KIND) problems.liked = "tooManyLiked";
  if (draft.improvable.length > MAX_PICKS_PER_KIND) problems.improvable = "tooManyImprovable";

  if (draft.liked.some((key) => findStatement(key)?.kind !== "Liked")) problems.liked = "unknownStatement";
  if (draft.improvable.some((key) => findStatement(key)?.kind !== "Improve")) problems.improvable = "unknownStatement";

  return problems;
}

/** Only call after validateReviewDraft returned no problems — the cast on employmentStatus relies on it. */
export function buildReviewRequest(draft: ReviewDraft): CompanyReviewRequest {
  return {
    employmentStatus: draft.employmentStatus as EmploymentStatus,
    overallRating: draft.overall,
    categoryRatings: OPTIONAL_CATEGORIES.flatMap((category) => {
      const rating = draft.ratings[category];
      return isOnScale(rating) ? [{ category, rating }] : [];
    }),
    likedStatements: [...draft.liked],
    improvableStatements: [...draft.improvable],
  };
}

/** The draft an edit form starts from. A legacy review contributes its overall rating and the
 *  three fixed ratings that map onto a current category; its text has no place in the new form,
 *  and its picks start empty. */
export function draftFromReview(review: MyCompanyReview): ReviewDraft {
  const ratings: Partial<Record<ReviewCategory, number>> = {};
  for (const { category, rating } of review.categoryRatings) {
    if (category !== "Overall") ratings[category] = rating;
  }
  return {
    employmentStatus: review.employmentStatus,
    overall: review.overallRating,
    ratings,
    liked: [...review.likedStatements],
    improvable: [...review.improvableStatements],
  };
}

/** The category a legacy rating field maps onto, for pages that still show legacy rows. */
export function legacyCategoryOf(field: keyof typeof LEGACY_CATEGORY_MAP): ReviewCategory {
  return LEGACY_CATEGORY_MAP[field];
}

/** What a rated row offers first: 4–5 stars lead with what was liked, 1–3 with what could be
 *  better. Nothing until there is a rating — the form is eleven star rows until then. */
export function suggestionsFor(category: ReviewCategory, rating: number): ReviewStatement[] {
  if (!isOnScale(rating)) return [];
  return statementsFor(category, rating >= 4 ? "Liked" : "Improve").slice(0, SUGGESTION_COUNT);
}

export function picksOf(draft: ReviewDraft, kind: ReviewStatementKind): string[] {
  return kind === "Liked" ? draft.liked : draft.improvable;
}

export function isCapReached(draft: ReviewDraft, kind: ReviewStatementKind): boolean {
  return picksOf(draft, kind).length >= MAX_PICKS_PER_KIND;
}

/** Adds or removes one statement in its own list. Refuses a pick past the cap or a key the
 *  catalogue does not know, returning the draft unchanged so the caller can render as is. */
export function togglePick(draft: ReviewDraft, key: string): ReviewDraft {
  const statement = findStatement(key);
  if (!statement) return draft;
  const list = picksOf(draft, statement.kind);
  const next = list.includes(key)
    ? list.filter((k) => k !== key)
    : list.length >= MAX_PICKS_PER_KIND
      ? list
      : [...list, key];
  if (next === list) return draft;
  return statement.kind === "Liked" ? { ...draft, liked: next } : { ...draft, improvable: next };
}

export type ReportDraftProblem = "noteRequiredForOther" | "noteTooLong";

/** "Other" with no explanation gives the admin nothing to act on — same rule as the server's. */
export function validateReportDraft(reason: ReviewReportReason, note: string): ReportDraftProblem | null {
  const trimmed = note.trim();
  if (reason === "Other" && trimmed.length === 0) return "noteRequiredForOther";
  if (trimmed.length > REPORT_NOTE_MAX_LENGTH) return "noteTooLong";
  return null;
}
