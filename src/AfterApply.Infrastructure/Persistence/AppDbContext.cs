using AfterApply.Domain.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Domain.Imports;
using AfterApply.Domain.Jobs;
using AfterApply.Domain.Notifications;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Identity;
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

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<ImportRowError> ImportRowErrors => Set<ImportRowError>();

    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<EmailConnection> EmailConnections => Set<EmailConnection>();

    public DbSet<EmailSuggestion> EmailSuggestions => Set<EmailSuggestion>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<TrackedJob> TrackedJobs => Set<TrackedJob>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
