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

    /// <summary>Root comments per page under a post; each comes with all its replies.</summary>
    public int CommentPageSize { get; init; } = 10;

    /// <summary>
    /// How old an upload has to be before a save may treat it as an orphan (not in the draft,
    /// not in the published version, not the cover) and delete it. The grace exists for one race:
    /// the editor uploads, then inserts the image — and a timer-driven autosave can land in
    /// between with HTML that does not mention the new image yet. That gap is a few hundred
    /// milliseconds (the upload's round trip); a minute covers it with room to spare, and keeps
    /// a swapped image from lingering in the bucket for long.
    /// </summary>
    public int OrphanMediaGraceMinutes { get; init; } = 1;

    /// <summary>The SEO suggestion's model settings. Bound from <c>Blog:Seo</c> (2026-09-21).</summary>
    public BlogSeoSettings Seo { get; init; } = new();

    public sealed class BlogSeoSettings
    {
        public const string HttpClientName = "vertex-blog-seo";

        /// <summary>Empty means unconfigured: the button answers a coded error rather than
        /// guessing a project (see CvScanOptions.CvReviewSettings.ProjectId).</summary>
        public string ProjectId { get; init; } = string.Empty;

        /// <summary>Pinned to the EU like the CV review — the same region the rest of the
        /// product's data lives in. Never <c>global</c>.</summary>
        public string Location { get; init; } = "europe-west1";

        /// <summary>Flash, not Flash-Lite: the job-fit eval (2026-09-14) found Lite inventing
        /// what was not in the text, and a keyword that is not in the post is worse than none.
        /// Thinking is switched off in the call — Flash answers empty with it on.</summary>
        public string Model { get; init; } = "gemini-2.5-flash";

        /// <summary>The body text the model sees. A post's first twelve thousand characters say
        /// what it is about; the cap bounds the per-click cost.</summary>
        public int MaxInputCharacters { get; init; } = 12_000;

        public int TimeoutSeconds { get; init; } = 25;
    }
}
