namespace AfterApply.Domain.EmailIntegrations;

/// <summary>Closed taxonomy for the disqualifying reason a rejection email states, if any — see
/// IEmailRejectionReasonExtractionProvider. Evidence-driven from a real mailbox audit
/// (2026-09-02, see DECISIONS.md): most rejections are boilerplate, so NotStated is the expected
/// majority outcome, not an edge case.</summary>
public enum RejectionReasonCategory
{
    /// <summary>No reason was stated, or the email only used generic/probabilistic language
    /// ("we typically look for...", "the most common reason we pass on candidates is...") that
    /// isn't a claim about this specific candidate.</summary>
    NotStated,
    LanguageRequirement,
    LocationOrRelocation,
    ExperienceLevelMismatch,
    SalaryExpectationMismatch,
    SkillOrTechStackGap,
    PositionCancelledOrFilled,
    CultureOrTeamFit,
    Other
}
