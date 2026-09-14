namespace AfterApply.Application.JobSources.Contracts;

/// <summary><c>PUT /api/job-sources/profile</c>. Replaces the whole profile: the titles list is
/// the new list, not a diff.</summary>
public sealed record UpsertJobSourceProfileRequest(
    IReadOnlyList<string> Titles,
    string Location,
    bool RemoteOnly = false,
    bool Enabled = true,
    // Postings scoring below this are hidden from the list (0 = show everything).
    int MinScore = 0,
    // Explicit consent to the default CV's text being sent to the scoring model. Required on
    // create; once recorded it stays recorded, so a later save may omit it.
    bool AcceptAiScoring = false,
    // The Monday "N postings are ready" e-mail.
    bool EmailDigest = true);

/// <summary>Admin-only (<c>PUT /api/admin/job-sources/settings/{userId}</c>). Null clears the
/// override back to the global default.</summary>
public sealed record UpdateUserJobSourceLimitsRequest(int? WeeklyPostingLimit);

/// <summary>Admin-only (<c>PUT /api/admin/pro/entitlements/{userId}</c>).</summary>
public sealed record GrantProEntitlementRequest(DateTimeOffset ActiveUntil);
