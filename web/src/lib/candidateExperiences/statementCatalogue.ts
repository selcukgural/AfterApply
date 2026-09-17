import type { ExperienceCategory, HiringOutcome, InterviewType, ProcessDuration, StageCount } from "@/types/api";
import { buildStatementCatalogue, type Statement } from "@/lib/statements/catalogue";

// Mirrors AfterApply.Domain.CandidateExperiences.ExperienceStatementCatalogue — the closed
// vocabulary a candidate experience can be made of. statementCatalogue.test.ts reads the C#
// source and fails if the two drift. Keys are `{prefix}.{pos|imp}.{slug}` and permanent; the
// wording lives under `candidateExperiences.statements.<key>.{label,sentence}` in the message
// catalogue, so the legal text can change without touching a stored entry.

export type ExperienceStatement = Statement<ExperienceCategory>;

/** Overall first (required), then the eight optional categories in the order the form and the
 *  summary panel show them. */
export const EXPERIENCE_CATEGORIES: readonly ExperienceCategory[] = [
  "Overall",
  "Communication",
  "ResponseTime",
  "Punctuality",
  "InterviewerPreparation",
  "QuestionRelevance",
  "Transparency",
  "AssignmentLoad",
  "OutcomeCommunication",
];

export const OPTIONAL_EXPERIENCE_CATEGORIES: readonly ExperienceCategory[] = EXPERIENCE_CATEGORIES.filter((c) => c !== "Overall");

export const EXPERIENCE_CATEGORY_PREFIX: Record<ExperienceCategory, string> = {
  Overall: "general",
  Communication: "communication",
  ResponseTime: "response",
  Punctuality: "punctuality",
  InterviewerPreparation: "preparation",
  QuestionRelevance: "questions",
  Transparency: "transparency",
  AssignmentLoad: "assignment",
  OutcomeCommunication: "outcome",
};

const SLUGS: Record<ExperienceCategory, { pos: readonly string[]; imp: readonly string[] }> = {
  Overall: {
    pos: ["positive_process", "respectful_approach", "clear_process", "professional_process", "would_apply_again"],
    imp: ["process", "candidate_experience", "predictability", "general_communication"],
  },
  Communication: {
    pos: ["steps_clear_upfront", "timely_information", "reachable_contact", "clear_expectations"],
    imp: ["step_information", "information_timing", "reachability", "clarity_of_expectations"],
  },
  ResponseTime: {
    pos: ["quick_replies", "clear_scheduling", "fast_scheduling"],
    imp: ["reply_time", "scheduling_speed", "waiting_between_stages"],
  },
  Punctuality: {
    pos: ["started_on_time", "duration_as_planned", "changes_announced"],
    imp: ["start_time", "interview_duration", "reschedule_notice"],
  },
  InterviewerPreparation: {
    pos: ["knew_my_cv", "interview_flow", "role_knowledge", "room_for_my_questions"],
    imp: ["preparation", "cv_familiarity", "interview_flow", "question_opportunity"],
  },
  QuestionRelevance: {
    pos: ["relevant_to_role", "practical_questions", "level_appropriate"],
    imp: ["relevance_to_role", "repeated_questions", "level_fit"],
  },
  Transparency: {
    pos: ["role_clarity", "salary_range_shared_early", "work_model_clear", "process_steps_clear"],
    imp: ["role_clarity", "salary_information_timing", "work_model_information", "posting_role_alignment"],
  },
  AssignmentLoad: {
    pos: ["reasonable_duration", "relevant_to_role", "feedback_received"],
    imp: ["duration", "scope", "feedback"],
  },
  OutcomeCommunication: {
    pos: ["timely_notification", "rejection_feedback", "kind_notification"],
    imp: ["notification", "notification_time", "feedback_content", "rejection_communication"],
  },
};

export const EXPERIENCE_CATALOGUE = buildStatementCatalogue<ExperienceCategory>(
  EXPERIENCE_CATEGORIES,
  EXPERIENCE_CATEGORY_PREFIX,
  SLUGS,
);

// The closed lists behind the process facts, in the order the form offers them. The two ordinal
// ones are in time order — the server's "typical" figures are medians over that order.
export const HIRING_OUTCOMES: readonly HiringOutcome[] = ["Offer", "Rejected", "InProgress", "Withdrew", "NoResponse"];
export const PROCESS_DURATIONS: readonly ProcessDuration[] = ["UnderOneWeek", "OneToTwoWeeks", "TwoToFourWeeks", "OneToTwoMonths", "OverTwoMonths"];
export const STAGE_COUNTS: readonly StageCount[] = ["One", "Two", "Three", "Four", "FivePlus"];
export const INTERVIEW_TYPES: readonly InterviewType[] = ["Phone", "Video", "OnSite", "TechnicalTest", "TakeHomeAssignment", "Panel", "AssessmentCenter"];
