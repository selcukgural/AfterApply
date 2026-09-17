using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Domain.CandidateExperiences;

/// <summary>
/// One candidate's rating of one company's hiring process — anonymous to readers, attributed to an
/// account in the database. The <c>(UserId, CompanyId)</c> pair is unique: a candidate gets one
/// voice per company and edits it rather than adding to it.
///
/// Nothing here was typed by the author: a required overall rating, optional per-category ratings
/// and picks from <see cref="ExperienceStatementCatalogue"/>, plus a few closed-list facts about
/// the process (outcome, duration, stages, interview types). It is published the moment it is
/// saved. Readers see the ratings, the picks, the facts and the quarter of <see cref="SubmittedAt"/>
/// — never the author, the exact date or a job title, because the company knows exactly whom it
/// interviewed in a given month.
/// </summary>
public sealed class CandidateExperience : AuditableEntity
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public Guid UserId { get; private set; }

    public Guid CompanyId { get; private set; }

    /// <summary>The one rating every experience has: it is the input to the company's candidate score.</summary>
    public int OverallRating { get; private set; }

    public HiringOutcome? Outcome { get; private set; }

    public ProcessDuration? Duration { get; private set; }

    public StageCount? Stages { get; private set; }

    /// <summary>When the current content was submitted — reset on every edit, so the public
    /// quarter label describes what is on screen, not the first draft.</summary>
    public DateTimeOffset SubmittedAt { get; private set; }

    private CandidateExperience()
    {
    }

    public static CandidateExperience Create(Guid userId, Guid companyId, CandidateExperienceContent content, DateTimeOffset now)
    {
        content.Validate();

        var experience = new CandidateExperience
        {
            UserId = userId,
            CompanyId = companyId,
            SubmittedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
        experience.Apply(content);
        return experience;
    }

    /// <summary>Replaces the scalar content (the caller rewrites the child rows).</summary>
    public void Edit(CandidateExperienceContent content, DateTimeOffset now)
    {
        content.Validate();
        Apply(content);
        SubmittedAt = now;
        Touch(now);
    }

    private void Apply(CandidateExperienceContent content)
    {
        OverallRating = content.OverallRating;
        Outcome = content.Outcome;
        Duration = content.Duration;
        Stages = content.Stages;
    }
}

public readonly record struct ExperienceCategoryRating(ExperienceCategory Category, int Rating);

/// <summary>
/// Everything an experience says, in one value so Create and Edit share one invariant. The
/// request validator says the same things earlier and in the user's language; this is the
/// boundary that stores the row, so it checks again — in particular that every statement key is
/// in the catalogue, because a key that is not would render as nothing on every card.
/// </summary>
public readonly record struct CandidateExperienceContent(
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRating> CategoryRatings,
    IReadOnlyList<string> Liked,
    IReadOnlyList<string> Improvable,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<InterviewType> InterviewTypes)
{
    public void Validate()
    {
        if (OverallRating is < CandidateExperience.MinRating or > CandidateExperience.MaxRating)
        {
            throw new CandidateExperienceContentInvalidException();
        }

        var seenCategories = new HashSet<ExperienceCategory>();
        foreach (var (category, rating) in CategoryRatings)
        {
            // Overall is the column, never a child row.
            if (category == ExperienceCategory.Overall || !Enum.IsDefined(category) || !seenCategories.Add(category)
                || rating is < CandidateExperience.MinRating or > CandidateExperience.MaxRating)
            {
                throw new CandidateExperienceContentInvalidException();
            }
        }

        ValidatePicks(Liked, ReviewStatementKind.Liked);
        ValidatePicks(Improvable, ReviewStatementKind.Improve);

        if ((Outcome is { } outcome && !Enum.IsDefined(outcome))
            || (Duration is { } duration && !Enum.IsDefined(duration))
            || (Stages is { } stages && !Enum.IsDefined(stages)))
        {
            throw new CandidateExperienceContentInvalidException();
        }

        var seenTypes = new HashSet<InterviewType>();
        foreach (var type in InterviewTypes)
        {
            if (!Enum.IsDefined(type) || !seenTypes.Add(type))
            {
                throw new CandidateExperienceContentInvalidException();
            }
        }
    }

    /// <summary>The catalogue entries behind the picks, in the author's order.</summary>
    public IEnumerable<ExperienceStatement> Statements()
    {
        foreach (var key in Liked.Concat(Improvable))
        {
            ExperienceStatementCatalogue.TryGet(key, out var statement);
            yield return statement;
        }
    }

    private static void ValidatePicks(IReadOnlyList<string> keys, ReviewStatementKind kind)
    {
        if (keys.Count > ExperienceStatementCatalogue.MaxPicksPerKind)
        {
            throw new CandidateExperienceContentInvalidException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (!ExperienceStatementCatalogue.TryGet(key, out var statement) || statement.Kind != kind || !seen.Add(key))
            {
                throw new CandidateExperienceContentInvalidException();
            }
        }
    }
}

public sealed class CandidateExperienceContentInvalidException()
    : DomainException("CANDIDATE_EXPERIENCE_CONTENT_INVALID",
        "The overall rating must be 1–5, category ratings 1–5 without repeats, statements at most 5 per kind from the catalogue, and process facts from their lists.");
