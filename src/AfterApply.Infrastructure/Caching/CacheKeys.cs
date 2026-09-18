namespace AfterApply.Infrastructure.Caching;

/// <summary>
/// The cache keys and tags more than one service has to agree on. A key that only its owning
/// service reads and evicts stays private to that service; what lives here is shared precisely
/// because a second writer (account deletion, an import, a status change) has to hit the same
/// string. Every entry below goes through FusionCache's backplane, so an eviction issued on one
/// Cloud Run instance lands in every other instance's L1 as well (DECISIONS.md 2026-09-18).
/// </summary>
internal static class CacheKeys
{
    /// <summary>Per-user status counts (<c>ApplicationService.GetSummaryCountsAsync</c>).</summary>
    public static string ApplicationsSummary(Guid userId) => $"applications:summary:{userId}";

    /// <summary>A personal access token's validation result, keyed by the token's hash.</summary>
    public static string PersonalAccessToken(string tokenHash) => $"pat:{tokenHash}";

    /// <summary>
    /// The reminders list: one entry per page, all carrying one tag per user, so the services that
    /// invalidate it — ReminderService on any answer, ApplicationService whenever a status change
    /// retires reminders, the scan job when it creates or retires them — drop every page with one
    /// call and never have to know which pages exist.
    /// </summary>
    public static class Reminders
    {
        public static string ActiveTag(Guid userId) => $"reminders:active:{userId}";

        public static string ActivePage(Guid userId, int page, int pageSize) => $"reminders:active:{userId}:p{page}:s{pageSize}";
    }

    /// <summary>
    /// Everything the public company pages read. One tag per company: every review, salary or
    /// candidate-experience write for that company — and account deletion, and enrichment —
    /// invalidates with a single <c>RemoveByTagAsync</c> instead of enumerating the pages, sorts
    /// and summaries that happen to be cached. <see cref="DirectoryTag"/> covers the lists that
    /// span companies (the directory, the sitemap's slug list).
    /// </summary>
    public static class Company
    {
        public static string Tag(Guid companyId) => $"company:{companyId}";

        public const string DirectoryTag = "directory";

        public static string PublicPage(Guid companyId) => $"company:public:{companyId}";

        public static string ReviewSummary(Guid companyId) => $"company-reviews:summary:{companyId}";

        public const string ReviewGlobalAverage = "company-reviews:global-average";

        public static string ReviewList(Guid companyId, int page, string sort) => $"company-reviews:list:{companyId}:p{page}:{sort}";

        public static string ExperienceSummary(Guid companyId) => $"candidate-experiences:summary:{companyId}";

        public const string ExperienceGlobalAverage = "candidate-experiences:global-average";

        public static string ExperienceList(Guid companyId, int page) => $"candidate-experiences:list:{companyId}:p{page}";

        public static string SalaryList(Guid companyId, int page) => $"company-salaries:list:{companyId}:p{page}";

        public static string DirectoryPage(int page) => $"company-directory:p{page}";

        public const string ReviewedSlugs = "company-reviews:reviewed-slugs";
    }
}
