namespace AfterApply.Domain.CandidateExperiences;

/// <summary>How many rounds the process had. Declaration order is the ordinal order (see
/// <see cref="ProcessDuration"/>): the "typical stage count" is a median over these.</summary>
public enum StageCount
{
    One,
    Two,
    Three,
    Four,
    FivePlus
}
