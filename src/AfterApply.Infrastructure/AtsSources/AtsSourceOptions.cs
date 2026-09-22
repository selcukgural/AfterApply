namespace AfterApply.Infrastructure.AtsSources;

/// <summary>
/// Reading a posting back from the ATS that hosted it, bound from the <c>AtsSources</c> section.
/// Off by default, like every other outbound-fetch feature here.
/// </summary>
public sealed class AtsSourceOptions
{
    public const string SectionName = "AtsSources";

    /// <summary><b>This flag and the privacy policy move together</b> — the same rule
    /// <c>JobSourceOptions.Enabled</c> carries. While it is true, a job URL a user captured is
    /// sent to that ATS's public API to read the posting back; the published extension privacy
    /// page and <c>/extension-privacy</c> name those recipients, and turning this on without that
    /// text in place would make the page wrong.
    ///
    /// Nothing about the user goes with the request: the URL is the posting's own public address,
    /// there is no account, no cookie and no identifier of ours in it.</summary>
    public bool Enabled { get; init; }

    /// <summary>A job is only enriched when its description is missing or shorter than this — the
    /// extension's own scrape is the primary source and a real description is left alone. Short
    /// enough to catch "Apply on the company site" stubs, long enough not to re-fetch a real
    /// posting that happens to be terse.</summary>
    public int MinDescriptionChars { get; init; } = 400;
}
