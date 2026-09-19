namespace AfterApply.Infrastructure.Blog;

public sealed class BlogOptions
{
    public const string SectionName = "Blog";

    /// <summary>Off → every blog endpoint answers 404 (the admin ones too), <c>/api/config</c>
    /// reports the feature off and the web app shows no trace of it — the
    /// <c>CandidateExperiences:Enabled</c> shape, so the feature can ship dark.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Cards per page on the public list.</summary>
    public int PageSize { get; init; } = 10;

    /// <summary>Rows per page on the admin table.</summary>
    public int AdminPageSize { get; init; } = 25;
}
