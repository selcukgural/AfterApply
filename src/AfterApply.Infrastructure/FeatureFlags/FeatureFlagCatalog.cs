using AfterApply.Application.FeatureFlags;
using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Infrastructure.AtsSources;
using AfterApply.Infrastructure.Blog;
using AfterApply.Infrastructure.Board;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.CvScan;
using AfterApply.Infrastructure.Documents;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Feedback;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.ResponseRates;
using AfterApply.Infrastructure.SilenceReports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.FeatureFlags;

/// <summary>
/// What each <see cref="FeatureFlag"/> is when nobody switched it (its options section's
/// <c>Enabled</c>, i.e. appsettings.json plus the deploy workflow's env vars), what it needs from
/// this deployment's configuration to work when on, and what else moves with it. Read once:
/// options are fixed for the life of the process.
/// </summary>
public sealed class FeatureFlagCatalog
{
    private sealed record Definition(
        Func<IServiceProvider, bool> Default,
        Func<IServiceProvider, string?> MissingPrerequisite,
        params FeatureFlagCoupling[] Couplings);

    private static string? None(IServiceProvider _) => null;

    private static T Get<T>(IServiceProvider services) where T : class =>
        services.GetRequiredService<IOptions<T>>().Value;

    /// <summary>One line per flag; <c>FeatureFlagCatalogTests</c> fails when a member is missing.</summary>
    private static readonly IReadOnlyDictionary<FeatureFlag, Definition> Definitions = new Dictionary<FeatureFlag, Definition>
    {
        [FeatureFlag.Board] = new(s => Get<BoardOptions>(s).Enabled, None),
        // Registration picks the bucket at start-up; with no bucket name the upload would have
        // nowhere to go (the same rule AddBlog enforces when the deploy turns the blog on).
        // Money: the editor's "suggest with AI" button calls Vertex.
        [FeatureFlag.Blog] = new(s => Get<BlogOptions>(s).Enabled, s =>
            Get<StorageOptions>(s) is { Provider: FileStorageProvider.GoogleCloudStorage } storage
            && string.IsNullOrWhiteSpace(storage.BlogMediaBucketName)
                ? FeatureFlagPrerequisites.BlogMediaBucket
                : null,
            FeatureFlagCoupling.Money),
        [FeatureFlag.CompanyReviews] = new(s => Get<CompanyReviewOptions>(s).Enabled, None),
        [FeatureFlag.CompanySalaries] = new(s => Get<CompanySalaryOptions>(s).Enabled, None),
        [FeatureFlag.CandidateExperiences] = new(s => Get<CandidateExperienceOptions>(s).Enabled, None),
        [FeatureFlag.CompanyIntelligence] = new(s => Get<CompanyIntelligenceOptions>(s).Enabled, None),
        [FeatureFlag.ResponseRates] = new(s => Get<ResponseRateOptions>(s).Enabled, None),
        [FeatureFlag.SilenceReports] = new(s => Get<SilenceReportOptions>(s).Enabled, None),
        [FeatureFlag.CvScan] = new(s => Get<CvScanOptions>(s).Enabled, None),
        [FeatureFlag.CvScanNotes] = new(s => Get<CvScanOptions>(s).LlmEnabled,
            s => string.IsNullOrWhiteSpace(Get<CvScanOptions>(s).Review.ProjectId) ? FeatureFlagPrerequisites.CvScanNotesProject : null,
            FeatureFlagCoupling.PrivacyText, FeatureFlagCoupling.Money),
        // What the rules cannot settle goes to OpenAI (United States), paid — /privacy names it.
        [FeatureFlag.EmailSignals] = new(s => Get<EmailForwardingOptions>(s).Enabled, None,
            FeatureFlagCoupling.PrivacyText, FeatureFlagCoupling.Money),
        [FeatureFlag.EmailAutoApproval] = new(s => Get<EmailAutoApprovalOptions>(s).Enabled, None),
        [FeatureFlag.FeedbackGitHub] = new(s => Get<FeedbackGitHubOptions>(s).Enabled,
            s => Get<FeedbackGitHubOptions>(s).HasTarget ? null : FeatureFlagPrerequisites.FeedbackGitHubTarget,
            FeatureFlagCoupling.PrivacyText),
        [FeatureFlag.AtsSources] = new(s => Get<AtsSourceOptions>(s).Enabled, None, FeatureFlagCoupling.PrivacyText),
        [FeatureFlag.JobSources] = new(s => Get<JobSourceOptions>(s).Enabled, None,
            FeatureFlagCoupling.PrivacyText, FeatureFlagCoupling.Money),
        [FeatureFlag.Payments] = new(s => Get<PayTrOptions>(s).Enabled,
            s => PayTrOptionsValidator.ProblemsWhenOn(Get<PayTrOptions>(s)).Count == 0 ? null : FeatureFlagPrerequisites.PayTrConfiguration,
            FeatureFlagCoupling.Money)
    };

    private readonly IReadOnlyDictionary<FeatureFlag, bool> _defaults;
    private readonly IReadOnlyDictionary<FeatureFlag, string?> _missing;

    public FeatureFlagCatalog(IServiceProvider services)
    {
        _defaults = Definitions.ToDictionary(d => d.Key, d => d.Value.Default(services));
        _missing = Definitions.ToDictionary(d => d.Key, d => d.Value.MissingPrerequisite(services));
    }

    public static IEnumerable<FeatureFlag> Defined => Definitions.Keys;

    public bool DefaultOf(FeatureFlag flag) => _defaults[flag];

    /// <summary>A <see cref="FeatureFlagPrerequisites"/> code, or null when the flag can be on.</summary>
    public string? MissingPrerequisiteOf(FeatureFlag flag) => _missing[flag];

    public IReadOnlyList<FeatureFlagCoupling> CouplingsOf(FeatureFlag flag) => Definitions[flag].Couplings;
}
