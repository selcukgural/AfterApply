import type {
  CandidateExperienceRequest,
  ExperienceCategory,
  HiringOutcome,
  InterviewType,
  MyCandidateExperience,
  ProcessDuration,
  StageCount,
} from "@/types/api";
import { EXPERIENCE_CATALOGUE, OPTIONAL_EXPERIENCE_CATEGORIES, type ExperienceStatement } from "@/lib/candidateExperiences/statementCatalogue";
import { isOnScale } from "@/lib/statements/catalogue";
import * as picks from "@/lib/statements/picks";

// Mirrors CandidateExperience's constants and CandidateExperienceRequestValidator on the server.
// The server is the one that decides; these exist so the form can say the same thing before a
// round trip.

/** What the form holds. `overall` is the one required rating; `ratings` holds only the optional
 *  categories the author has rated (0 or absent = left blank); the pick lists are catalogue keys
 *  in pick order; the facts are "" for "not said". */
export interface ExperienceDraft {
  overall: number;
  ratings: Partial<Record<ExperienceCategory, number>>;
  liked: string[];
  improvable: string[];
  outcome: HiringOutcome | "";
  duration: ProcessDuration | "";
  stages: StageCount | "";
  interviewTypes: InterviewType[];
}

export const EMPTY_EXPERIENCE_DRAFT: ExperienceDraft = {
  overall: 0,
  ratings: {},
  liked: [],
  improvable: [],
  outcome: "",
  duration: "",
  stages: "",
  interviewTypes: [],
};

/** Translation keys under `candidateExperiences.form.problems`. */
export type ExperienceDraftProblem = "overallRequired" | "ratingInvalid" | picks.PickProblem;

export type ExperienceDraftField = "overall" | "liked" | "improvable" | ExperienceCategory;

/** Every problem at once, keyed by field, so the form can mark each field rather than the first. */
export function validateExperienceDraft(draft: ExperienceDraft): Partial<Record<ExperienceDraftField, ExperienceDraftProblem>> {
  const problems: Partial<Record<ExperienceDraftField, ExperienceDraftProblem>> = {};

  if (!isOnScale(draft.overall)) {
    problems.overall = "overallRequired";
  }

  for (const category of OPTIONAL_EXPERIENCE_CATEGORIES) {
    const value = draft.ratings[category];
    // Absent or zero is "left blank" and fine; anything else must be on the scale.
    if (value !== undefined && value !== 0 && !isOnScale(value)) {
      problems[category] = "ratingInvalid";
    }
  }

  Object.assign(problems, picks.validatePicks(EXPERIENCE_CATALOGUE, draft));

  return problems;
}

/** Only call after validateExperienceDraft returned no problems. A fact left at "" is sent as
 *  null — the server stores "not said" as null, never as a default value. */
export function buildExperienceRequest(draft: ExperienceDraft): CandidateExperienceRequest {
  return {
    overallRating: draft.overall,
    categoryRatings: OPTIONAL_EXPERIENCE_CATEGORIES.flatMap((category) => {
      const rating = draft.ratings[category];
      return isOnScale(rating) ? [{ category, rating }] : [];
    }),
    likedStatements: [...draft.liked],
    improvableStatements: [...draft.improvable],
    outcome: draft.outcome === "" ? null : draft.outcome,
    duration: draft.duration === "" ? null : draft.duration,
    stages: draft.stages === "" ? null : draft.stages,
    interviewTypes: [...draft.interviewTypes],
  };
}

/** The draft an edit form starts from. */
export function draftFromExperience(experience: MyCandidateExperience): ExperienceDraft {
  const ratings: Partial<Record<ExperienceCategory, number>> = {};
  for (const { category, rating } of experience.categoryRatings) {
    if (category !== "Overall") ratings[category] = rating;
  }
  return {
    overall: experience.overallRating,
    ratings,
    liked: [...experience.likedStatements],
    improvable: [...experience.improvableStatements],
    outcome: experience.outcome ?? "",
    duration: experience.duration ?? "",
    stages: experience.stages ?? "",
    interviewTypes: [...experience.interviewTypes],
  };
}

export function suggestionsFor(category: ExperienceCategory, rating: number): ExperienceStatement[] {
  return picks.suggestionsFor(EXPERIENCE_CATALOGUE, category, rating);
}

export function isCapReached(draft: ExperienceDraft, kind: "Liked" | "Improve"): boolean {
  return picks.isCapReached(draft, kind);
}

export function togglePick(draft: ExperienceDraft, key: string): ExperienceDraft {
  return picks.togglePick(EXPERIENCE_CATALOGUE, draft, key);
}

/** Adds or removes one interview type, keeping catalogue order so the request is stable. */
export function toggleInterviewType(draft: ExperienceDraft, type: InterviewType, order: readonly InterviewType[]): ExperienceDraft {
  const next = draft.interviewTypes.includes(type)
    ? draft.interviewTypes.filter((t) => t !== type)
    : order.filter((t) => t === type || draft.interviewTypes.includes(t));
  return { ...draft, interviewTypes: next };
}

/** `2026-Q3` → "2026 Ç3" / "2026 Q3". The quarter is all a reader ever sees of the date. */
export function formatQuarter(quarter: string, locale: string): string {
  const match = /^(\d{4})-Q([1-4])$/.exec(quarter);
  if (!match) return quarter;
  const [, year, q] = match;
  return locale.startsWith("tr") ? `${year} Ç${q}` : `${year} Q${q}`;
}
