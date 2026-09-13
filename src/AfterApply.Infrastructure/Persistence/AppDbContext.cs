using AfterApply.Domain.Applications;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Companies;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Domain.Feedback;
using AfterApply.Domain.Imports;
using AfterApply.Domain.Jobs;
using AfterApply.Domain.Mailing;
using AfterApply.Domain.Metrics;
using AfterApply.Domain.Notifications;
using AfterApply.Domain.SiteTraffic;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Persistence.Converters;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<Company> Companies => Set<Company>();

    public DbSet<Job> Jobs => Set<Job>();

    public DbSet<DomainApplication> Applications => Set<DomainApplication>();

    public DbSet<ApplicationEvent> ApplicationEvents => Set<ApplicationEvent>();

    public DbSet<ApplicationStatusHistory> ApplicationStatusHistories => Set<ApplicationStatusHistory>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PersonalAccessToken> PersonalAccessTokens => Set<PersonalAccessToken>();

    public DbSet<ExtensionPairingRequest> ExtensionPairingRequests => Set<ExtensionPairingRequest>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<EmailConnection> EmailConnections => Set<EmailConnection>();

    public DbSet<EmailSuggestion> EmailSuggestions => Set<EmailSuggestion>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<TrackedJob> TrackedJobs => Set<TrackedJob>();

    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    public DbSet<CvDocument> CvDocuments => Set<CvDocument>();

    public DbSet<FeedbackEntry> FeedbackEntries => Set<FeedbackEntry>();

    public DbSet<ProductMetricsDailySnapshot> ProductMetricsDailySnapshots => Set<ProductMetricsDailySnapshot>();

    public DbSet<SiteTrafficDailyCounter> SiteTrafficDailyCounters => Set<SiteTrafficDailyCounter>();

    public DbSet<BenchmarkSubmission> BenchmarkSubmissions => Set<BenchmarkSubmission>();

    public DbSet<CvScanResult> CvScanResults => Set<CvScanResult>();

    public DbSet<CompanyReview> CompanyReviews => Set<CompanyReview>();

    public DbSet<CompanyReviewReport> CompanyReviewReports => Set<CompanyReviewReport>();

    public DbSet<CompanyReviewHelpfulMark> CompanyReviewHelpfulMarks => Set<CompanyReviewHelpfulMark>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    // Every timestamp in the model goes to Postgres in UTC, in one place rather than per property:
    // Npgsql rejects any other offset on a timestamptz write, and the offsets arrive from outside —
    // a hand-written API payload, an imported CSV row, an extension on a machine that is not on UTC.
    // See UtcDateTimeOffsetConverter for why this normalises rather than rejects. The store type is
    // unchanged (timestamptz either way), so this needs no migration.
    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        base.ConfigureConventions(builder);
        builder.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();
    }
}
