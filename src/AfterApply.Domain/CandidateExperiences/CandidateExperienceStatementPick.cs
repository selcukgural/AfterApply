using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>One predefined statement a candidate picked. Only the catalogue key is stored — the
/// wording is looked up at render time, so a legal revision of a sentence changes every
/// experience that picked it without a data migration.</summary>
public sealed class CandidateExperienceStatementPick : Entity
{
    public const int MaxKeyLength = 80;

    public Guid ExperienceId { get; private set; }

    public string StatementKey { get; private set; } = string.Empty;

    public ReviewStatementKind Kind { get; private set; }

    private CandidateExperienceStatementPick()
    {
    }

    public static CandidateExperienceStatementPick Create(Guid experienceId, ExperienceStatement statement) =>
        new() { ExperienceId = experienceId, StatementKey = statement.Key, Kind = statement.Kind };
}
