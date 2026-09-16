import type { ReviewCategory, ReviewStatementKind } from "@/types/api";

// Mirrors AfterApply.Domain.CompanyReviews.ReviewStatementCatalogue — the closed vocabulary a
// review can be made of. statementCatalogue.test.ts reads the C# source and fails if the two
// drift. Keys are `{prefix}.{pos|imp}.{slug}` and permanent; the wording lives under
// `companyReviews.statements.<key>.{label,sentence}` in the message catalogue, so the legal text
// can change without touching a stored review.

export interface ReviewStatement {
  key: string;
  category: ReviewCategory;
  kind: ReviewStatementKind;
}

/** Per review, per kind — the server refuses more. */
export const MAX_PICKS_PER_KIND = 5;

/** How many statements a rated row offers before "show more". */
export const SUGGESTION_COUNT = 3;

/** Overall first (required), then the ten optional categories in the order the form and the
 *  summary panel show them. */
export const REVIEW_CATEGORIES: readonly ReviewCategory[] = [
  "Overall",
  "WorkEnvironment",
  "Management",
  "CareerGrowth",
  "WorkLifeBalance",
  "Pay",
  "Benefits",
  "RemoteWork",
  "Tooling",
  "Hiring",
  "Onboarding",
];

export const OPTIONAL_CATEGORIES: readonly ReviewCategory[] = REVIEW_CATEGORIES.filter((c) => c !== "Overall");

export const CATEGORY_PREFIX: Record<ReviewCategory, string> = {
  Overall: "general",
  WorkEnvironment: "environment",
  Management: "management",
  CareerGrowth: "career",
  WorkLifeBalance: "balance",
  Pay: "pay",
  Benefits: "benefits",
  RemoteWork: "remote",
  Tooling: "tooling",
  Hiring: "hiring",
  Onboarding: "onboarding",
};

const SLUGS: Record<ReviewCategory, { pos: readonly string[]; imp: readonly string[] }> = {
  Overall: {
    pos: ["positive_experience", "satisfied_working_here", "positive_environment", "positive_employee_experience", "growth_opportunities"],
    imp: ["some_working_conditions", "employee_experience", "internal_processes", "work_environment", "room_to_improve"],
  },
  WorkEnvironment: {
    pos: ["positive_environment", "suitable_for_employees", "team_collaboration", "team_communication", "mutual_support", "teamwork_encouraged", "motivating", "easy_to_communicate", "cross_team_collaboration", "general_communication"],
    imp: ["work_environment", "team_communication", "cross_team_communication", "collaboration_processes", "knowledge_sharing", "employee_communication", "employee_experience", "teamwork_practices", "cross_team_coordination", "communication_processes"],
  },
  Management: {
    pos: ["communication_with_management", "managers_accessible", "opinions_considered", "feedback_culture", "manager_employee_communication", "informed_about_decisions", "clear_expectations", "management_employee_communication", "feedback_easy", "management_approach"],
    imp: ["management_employee_communication", "consideration_of_opinions", "feedback_processes", "managers_communication", "information_about_decisions", "clarity_of_expectations", "access_to_managers", "handling_of_feedback", "management_team_communication", "internal_communication"],
  },
  CareerGrowth: {
    pos: ["career_opportunities", "career_growth_supported", "promotion_process", "development_valued", "new_responsibilities", "career_goals_supported", "skill_development", "career_guidance", "internal_career_paths", "professional_development", "training_opportunities", "learning_supported", "technical_growth", "new_skills", "training_resources", "self_development", "learning_culture", "training_programs", "vocational_development", "new_technologies"],
    imp: ["career_opportunities", "promotion_transparency", "career_paths", "development_opportunities", "new_responsibilities", "career_goal_support", "professional_development", "visibility_of_opportunities", "promotion_criteria", "development_programs", "training_opportunities", "technical_training", "vocational_programs", "training_budget", "skill_programs", "training_variety", "learning_support", "technical_growth_opportunities", "training_scope", "development_resources"],
  },
  WorkLifeBalance: {
    pos: ["balanced_workload", "working_hours", "predictable_hours", "organized_planning", "fair_task_distribution", "work_pace", "work_life_balance", "low_overtime", "planned_progress", "work_arrangement"],
    imp: ["workload_distribution", "predictability_of_hours", "work_planning", "task_distribution", "work_life_balance", "work_pace", "overtime", "cross_team_workload", "prioritisation", "work_arrangement"],
  },
  Pay: {
    pos: ["salary_level", "pay_policy", "matches_responsibilities", "matches_market", "raise_process", "performance_link", "adequate_for_employees", "total_compensation"],
    imp: ["salary_level", "pay_policy", "alignment_with_responsibilities", "alignment_with_market", "raise_process", "performance_link", "alignment_with_expectations", "total_compensation"],
  },
  Benefits: {
    pos: ["benefits", "health", "meals", "transport", "leave", "package_sufficient", "social_benefits", "meets_needs"],
    imp: ["benefits", "health", "meals", "transport", "leave", "package_scope", "social_benefits", "variety"],
  },
  RemoteWork: {
    pos: ["remote_work", "hybrid_model", "flexible_model", "location_flexibility", "remote_setup", "flexible_hours", "remote_teamwork", "digital_tools", "fits_needs", "flexibility"],
    imp: ["remote_work", "hybrid_options", "model_flexibility", "hours_flexibility", "remote_setup", "digital_tools", "remote_team_processes", "fit_to_needs", "flexible_work", "remote_processes"],
  },
  Tooling: {
    pos: ["technologies", "infrastructure", "tools", "keeps_up", "tools_provided", "development_process", "technical_team_conditions", "technical_growth_environment"],
    imp: ["infrastructure", "tools", "technology_currency", "technical_resources", "development_process", "technical_team_conditions", "technology_investment", "technical_growth_support"],
  },
  Hiring: {
    pos: ["hiring_process", "clear_process", "interview_process", "professional_interviews", "role_information", "planned_process", "communication", "candidate_information"],
    imp: ["hiring_process", "role_information", "interview_transparency", "candidate_communication", "planning", "process_information", "stage_clarity", "candidate_feedback"],
  },
  Onboarding: {
    pos: ["onboarding", "orientation", "first_days_briefing", "newcomer_support", "adaptation", "planned_start"],
    imp: ["orientation", "planned_start", "briefing", "adaptation_support", "first_days_guidance", "newcomer_training"],
  },
};

export const STATEMENTS: readonly ReviewStatement[] = REVIEW_CATEGORIES.flatMap((category) => [
  ...SLUGS[category].pos.map((slug) => ({ key: `${CATEGORY_PREFIX[category]}.pos.${slug}`, category, kind: "Liked" as const })),
  ...SLUGS[category].imp.map((slug) => ({ key: `${CATEGORY_PREFIX[category]}.imp.${slug}`, category, kind: "Improve" as const })),
]);

const BY_KEY = new Map(STATEMENTS.map((s) => [s.key, s]));

export function findStatement(key: string): ReviewStatement | undefined {
  return BY_KEY.get(key);
}

export function statementsFor(category: ReviewCategory, kind: ReviewStatementKind): ReviewStatement[] {
  return STATEMENTS.filter((s) => s.category === category && s.kind === kind);
}

/** The legacy form's fixed ratings that map onto a current category; salary & benefits maps onto
 *  none and is shown under its own legacy label. */
export const LEGACY_CATEGORY_MAP = {
  managementRating: "Management",
  workEnvironmentRating: "WorkEnvironment",
  careerAndDevelopmentRating: "CareerGrowth",
} as const satisfies Record<string, ReviewCategory>;

/** `companyReviews.categories.<key>` — the enum name with a lower-case first letter. */
export function categoryMessageKey(category: ReviewCategory): string {
  return category[0].toLowerCase() + category.slice(1);
}
