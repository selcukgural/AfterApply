namespace AfterApply.Domain.Applications;

/// <summary>
/// How the candidate learned of a rejection — the difference between a company that told them and
/// one whose "no" had to be found on a portal or guessed. Asked once, optionally, when the status
/// becomes Rejected; a rejection that arrived as a matched email is <see cref="CompanyNotified"/>
/// without asking. Feeds the "tells candidates it said no" rate (Greenhouse's "Communicative"
/// criterion is exactly this: rejection emails sent).
/// </summary>
public enum RejectionNotice
{
    /// <summary>By email or message addressed to the candidate.</summary>
    CompanyNotified,

    /// <summary>Seen as a status on the job portal or the application page — nobody wrote.</summary>
    SeenOnPortal,

    /// <summary>Heard some other way, or the candidate is inferring it.</summary>
    OtherOrInferred
}
