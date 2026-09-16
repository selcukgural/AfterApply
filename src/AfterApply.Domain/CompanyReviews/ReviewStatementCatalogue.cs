using System.Collections.Frozen;

namespace AfterApply.Domain.CompanyReviews;

/// <summary>One predefined statement a reviewer can pick. The key is the only thing a row stores;
/// the wording (Turkish and English) lives in the web's message catalogue under the same key, so
/// the legal text can be revised without touching a single stored review.</summary>
public sealed record ReviewStatement(string Key, ReviewCategory Category, ReviewStatementKind Kind);

/// <summary>
/// The closed vocabulary of things a reviewer can say about a company. Free text was dropped on
/// purpose (see DECISIONS.md 2026-09-16): every sentence here was written to be low-risk under
/// Turkish defamation law, and a review can only ever be a selection from this list.
///
/// Keys are <c>{prefix}.{pos|imp}.{slug}</c> and are permanent identifiers — rename the wording,
/// never the key. The web test <c>statementCatalogue.test.ts</c> reads the slugs straight out of
/// this file, so keep the <c>Liked(...)</c>/<c>Improve(...)</c> call shape.
/// </summary>
public static class ReviewStatementCatalogue
{
    /// <summary>Per review, per kind: enough to say what stood out, few enough to read on a card.</summary>
    public const int MaxPicksPerKind = 5;

    public static IReadOnlyList<ReviewStatement> All { get; } =
    [
        // Genel değerlendirme
        ..Liked(ReviewCategory.Overall,
            "positive_experience", "satisfied_working_here", "positive_environment", "positive_employee_experience", "growth_opportunities"),
        ..Improve(ReviewCategory.Overall,
            "some_working_conditions", "employee_experience", "internal_processes", "work_environment", "room_to_improve"),
        // Çalışma ortamı
        ..Liked(ReviewCategory.WorkEnvironment,
            "positive_environment", "suitable_for_employees", "team_collaboration", "team_communication", "mutual_support", "teamwork_encouraged", "motivating", "easy_to_communicate", "cross_team_collaboration", "general_communication"),
        ..Improve(ReviewCategory.WorkEnvironment,
            "work_environment", "team_communication", "cross_team_communication", "collaboration_processes", "knowledge_sharing", "employee_communication", "employee_experience", "teamwork_practices", "cross_team_coordination", "communication_processes"),
        // Yönetim ve iletişim
        ..Liked(ReviewCategory.Management,
            "communication_with_management", "managers_accessible", "opinions_considered", "feedback_culture", "manager_employee_communication", "informed_about_decisions", "clear_expectations", "management_employee_communication", "feedback_easy", "management_approach"),
        ..Improve(ReviewCategory.Management,
            "management_employee_communication", "consideration_of_opinions", "feedback_processes", "managers_communication", "information_about_decisions", "clarity_of_expectations", "access_to_managers", "handling_of_feedback", "management_team_communication", "internal_communication"),
        // Kariyer gelişimi
        ..Liked(ReviewCategory.CareerGrowth,
            "career_opportunities", "career_growth_supported", "promotion_process", "development_valued", "new_responsibilities", "career_goals_supported", "skill_development", "career_guidance", "internal_career_paths", "professional_development", "training_opportunities", "learning_supported", "technical_growth", "new_skills", "training_resources", "self_development", "learning_culture", "training_programs", "vocational_development", "new_technologies"),
        ..Improve(ReviewCategory.CareerGrowth,
            "career_opportunities", "promotion_transparency", "career_paths", "development_opportunities", "new_responsibilities", "career_goal_support", "professional_development", "visibility_of_opportunities", "promotion_criteria", "development_programs", "training_opportunities", "technical_training", "vocational_programs", "training_budget", "skill_programs", "training_variety", "learning_support", "technical_growth_opportunities", "training_scope", "development_resources"),
        // İş-özel hayat dengesi / çalışma saatleri
        ..Liked(ReviewCategory.WorkLifeBalance,
            "balanced_workload", "working_hours", "predictable_hours", "organized_planning", "fair_task_distribution", "work_pace", "work_life_balance", "low_overtime", "planned_progress", "work_arrangement"),
        ..Improve(ReviewCategory.WorkLifeBalance,
            "workload_distribution", "predictability_of_hours", "work_planning", "task_distribution", "work_life_balance", "work_pace", "overtime", "cross_team_workload", "prioritisation", "work_arrangement"),
        // Ücret
        ..Liked(ReviewCategory.Pay,
            "salary_level", "pay_policy", "matches_responsibilities", "matches_market", "raise_process", "performance_link", "adequate_for_employees", "total_compensation"),
        ..Improve(ReviewCategory.Pay,
            "salary_level", "pay_policy", "alignment_with_responsibilities", "alignment_with_market", "raise_process", "performance_link", "alignment_with_expectations", "total_compensation"),
        // Yan haklar
        ..Liked(ReviewCategory.Benefits,
            "benefits", "health", "meals", "transport", "leave", "package_sufficient", "social_benefits", "meets_needs"),
        ..Improve(ReviewCategory.Benefits,
            "benefits", "health", "meals", "transport", "leave", "package_scope", "social_benefits", "variety"),
        // Uzaktan / hibrit çalışma
        ..Liked(ReviewCategory.RemoteWork,
            "remote_work", "hybrid_model", "flexible_model", "location_flexibility", "remote_setup", "flexible_hours", "remote_teamwork", "digital_tools", "fits_needs", "flexibility"),
        ..Improve(ReviewCategory.RemoteWork,
            "remote_work", "hybrid_options", "model_flexibility", "hours_flexibility", "remote_setup", "digital_tools", "remote_team_processes", "fit_to_needs", "flexible_work", "remote_processes"),
        // Teknoloji ve çalışma araçları
        ..Liked(ReviewCategory.Tooling,
            "technologies", "infrastructure", "tools", "keeps_up", "tools_provided", "development_process", "technical_team_conditions", "technical_growth_environment"),
        ..Improve(ReviewCategory.Tooling,
            "infrastructure", "tools", "technology_currency", "technical_resources", "development_process", "technical_team_conditions", "technology_investment", "technical_growth_support"),
        // İşe alım süreci
        ..Liked(ReviewCategory.Hiring,
            "hiring_process", "clear_process", "interview_process", "professional_interviews", "role_information", "planned_process", "communication", "candidate_information"),
        ..Improve(ReviewCategory.Hiring,
            "hiring_process", "role_information", "interview_transparency", "candidate_communication", "planning", "process_information", "stage_clarity", "candidate_feedback"),
        // Onboarding / işe başlangıç
        ..Liked(ReviewCategory.Onboarding,
            "onboarding", "orientation", "first_days_briefing", "newcomer_support", "adaptation", "planned_start"),
        ..Improve(ReviewCategory.Onboarding,
            "orientation", "planned_start", "briefing", "adaptation_support", "first_days_guidance", "newcomer_training"),
    ];

    private static readonly FrozenDictionary<string, ReviewStatement> ByKey =
        All.ToFrozenDictionary(s => s.Key, StringComparer.Ordinal);

    public static bool TryGet(string key, out ReviewStatement statement) => ByKey.TryGetValue(key, out statement!);

    public static IEnumerable<ReviewStatement> For(ReviewCategory category, ReviewStatementKind kind) =>
        All.Where(s => s.Category == category && s.Kind == kind);

    /// <summary>The segment a category contributes to its statements' keys.</summary>
    public static string Prefix(ReviewCategory category) => category switch
    {
        ReviewCategory.Overall => "general",
        ReviewCategory.WorkEnvironment => "environment",
        ReviewCategory.Management => "management",
        ReviewCategory.CareerGrowth => "career",
        ReviewCategory.WorkLifeBalance => "balance",
        ReviewCategory.Pay => "pay",
        ReviewCategory.Benefits => "benefits",
        ReviewCategory.RemoteWork => "remote",
        ReviewCategory.Tooling => "tooling",
        ReviewCategory.Hiring => "hiring",
        ReviewCategory.Onboarding => "onboarding",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    private static ReviewStatement[] Liked(ReviewCategory category, params string[] slugs) => Build(category, ReviewStatementKind.Liked, "pos", slugs);

    private static ReviewStatement[] Improve(ReviewCategory category, params string[] slugs) => Build(category, ReviewStatementKind.Improve, "imp", slugs);

    private static ReviewStatement[] Build(ReviewCategory category, ReviewStatementKind kind, string segment, string[] slugs) =>
        slugs.Select(slug => new ReviewStatement($"{Prefix(category)}.{segment}.{slug}", category, kind)).ToArray();
}
