using System.Collections.Frozen;
using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>One predefined statement a candidate can pick. The key is the only thing a row stores;
/// the wording (Turkish and English) lives in the web's message catalogue under the same key, so
/// the legal text can be revised without touching a single stored experience.</summary>
public sealed record ExperienceStatement(string Key, ExperienceCategory Category, ReviewStatementKind Kind);

/// <summary>
/// The closed vocabulary of things a candidate can say about a hiring process. Free text was never
/// part of this feature (see DECISIONS.md 2026-09-17, same reasoning as the structured reviews):
/// every sentence here is an opinion about the author's own process, written to be low-risk under
/// Turkish defamation law, and an experience can only ever be a selection from this list.
///
/// Keys are <c>{prefix}.{pos|imp}.{slug}</c> and are permanent identifiers — rename the wording,
/// never the key. Prefixes are disjoint from <see cref="ReviewStatementCatalogue"/>'s, so the two
/// vocabularies can never be confused in a message catalogue. The web test
/// <c>statementCatalogue.test.ts</c> reads the slugs straight out of this file, so keep the
/// <c>Liked(...)</c>/<c>Improve(...)</c> call shape.
/// </summary>
public static class ExperienceStatementCatalogue
{
    /// <summary>Per experience, per kind: enough to say what stood out, few enough to read on a card.</summary>
    public const int MaxPicksPerKind = 5;

    public static IReadOnlyList<ExperienceStatement> All { get; } =
    [
        // Genel değerlendirme
        ..Liked(ExperienceCategory.Overall,
            "positive_process", "respectful_approach", "clear_process", "professional_process", "would_apply_again"),
        ..Improve(ExperienceCategory.Overall,
            "process", "candidate_experience", "predictability", "general_communication"),
        // İletişim ve bilgilendirme
        ..Liked(ExperienceCategory.Communication,
            "steps_clear_upfront", "timely_information", "reachable_contact", "clear_expectations"),
        ..Improve(ExperienceCategory.Communication,
            "step_information", "information_timing", "reachability", "clarity_of_expectations"),
        // Yanıt hızı
        ..Liked(ExperienceCategory.ResponseTime,
            "quick_replies", "clear_scheduling", "fast_scheduling"),
        ..Improve(ExperienceCategory.ResponseTime,
            "reply_time", "scheduling_speed", "waiting_between_stages"),
        // Görüşme zamanına saygı
        ..Liked(ExperienceCategory.Punctuality,
            "started_on_time", "duration_as_planned", "changes_announced"),
        ..Improve(ExperienceCategory.Punctuality,
            "start_time", "interview_duration", "reschedule_notice"),
        // Görüşmecilerin hazırlığı
        ..Liked(ExperienceCategory.InterviewerPreparation,
            "knew_my_cv", "interview_flow", "role_knowledge", "room_for_my_questions"),
        ..Improve(ExperienceCategory.InterviewerPreparation,
            "preparation", "cv_familiarity", "interview_flow", "question_opportunity"),
        // Soruların role uygunluğu
        ..Liked(ExperienceCategory.QuestionRelevance,
            "relevant_to_role", "practical_questions", "level_appropriate"),
        ..Improve(ExperienceCategory.QuestionRelevance,
            "relevance_to_role", "repeated_questions", "level_fit"),
        // Rol ve ücret hakkında şeffaflık
        ..Liked(ExperienceCategory.Transparency,
            "role_clarity", "salary_range_shared_early", "work_model_clear", "process_steps_clear"),
        ..Improve(ExperienceCategory.Transparency,
            "role_clarity", "salary_information_timing", "work_model_information", "posting_role_alignment"),
        // Ödev / case yükü
        ..Liked(ExperienceCategory.AssignmentLoad,
            "reasonable_duration", "relevant_to_role", "feedback_received"),
        ..Improve(ExperienceCategory.AssignmentLoad,
            "duration", "scope", "feedback"),
        // Sonuç bildirimi ve geri bildirim
        ..Liked(ExperienceCategory.OutcomeCommunication,
            "timely_notification", "rejection_feedback", "kind_notification"),
        ..Improve(ExperienceCategory.OutcomeCommunication,
            "notification", "notification_time", "feedback_content", "rejection_communication"),
    ];

    private static readonly FrozenDictionary<string, ExperienceStatement> ByKey =
        All.ToFrozenDictionary(s => s.Key, StringComparer.Ordinal);

    public static bool TryGet(string key, out ExperienceStatement statement) => ByKey.TryGetValue(key, out statement!);

    public static IEnumerable<ExperienceStatement> For(ExperienceCategory category, ReviewStatementKind kind) =>
        All.Where(s => s.Category == category && s.Kind == kind);

    /// <summary>The segment a category contributes to its statements' keys.</summary>
    public static string Prefix(ExperienceCategory category) => category switch
    {
        ExperienceCategory.Overall => "general",
        ExperienceCategory.Communication => "communication",
        ExperienceCategory.ResponseTime => "response",
        ExperienceCategory.Punctuality => "punctuality",
        ExperienceCategory.InterviewerPreparation => "preparation",
        ExperienceCategory.QuestionRelevance => "questions",
        ExperienceCategory.Transparency => "transparency",
        ExperienceCategory.AssignmentLoad => "assignment",
        ExperienceCategory.OutcomeCommunication => "outcome",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
    };

    private static ExperienceStatement[] Liked(ExperienceCategory category, params string[] slugs) => Build(category, ReviewStatementKind.Liked, "pos", slugs);

    private static ExperienceStatement[] Improve(ExperienceCategory category, params string[] slugs) => Build(category, ReviewStatementKind.Improve, "imp", slugs);

    private static ExperienceStatement[] Build(ExperienceCategory category, ReviewStatementKind kind, string segment, string[] slugs) =>
        slugs.Select(slug => new ExperienceStatement($"{Prefix(category)}.{segment}.{slug}", category, kind)).ToArray();
}
