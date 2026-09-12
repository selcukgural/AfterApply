using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

// The user-owned side of the job-source module. Every table here cascades from the account — see
// ApplicationConfiguration for why that is a foreign key and not a line in DeleteAccountAsync.

public sealed class UserJobSourceProfileConfiguration : IEntityTypeConfiguration<UserJobSourceProfile>
{
    public void Configure(EntityTypeBuilder<UserJobSourceProfile> builder)
    {
        builder.ToTable("UserJobSourceProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Location).IsRequired().HasMaxLength(JobSourceQuery.MaxLocationLength);

        builder.HasIndex(p => p.UserId).IsUnique();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Queries).WithOne().HasForeignKey(q => q.ProfileId).OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Queries).AutoInclude(false);
        // A read-only view over Queries, not a second navigation.
        builder.Ignore(p => p.OrderedQueries);
    }
}

public sealed class UserJobSourceProfileQueryConfiguration : IEntityTypeConfiguration<UserJobSourceProfileQuery>
{
    public void Configure(EntityTypeBuilder<UserJobSourceProfileQuery> builder)
    {
        builder.ToTable("UserJobSourceProfileQueries");
        builder.HasKey(q => new { q.ProfileId, q.QueryId });

        builder.Property(q => q.Title).IsRequired().HasMaxLength(JobSourceQuery.MaxKeywordsLength);

        builder.HasOne<JobSourceQuery>().WithMany().HasForeignKey(q => q.QueryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserJobSourceSettingsConfiguration : IEntityTypeConfiguration<UserJobSourceSettings>
{
    public void Configure(EntityTypeBuilder<UserJobSourceSettings> builder)
    {
        builder.ToTable("UserJobSourceSettings");
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => s.UserId).IsUnique();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserJobSourceDeliveryConfiguration : IEntityTypeConfiguration<UserJobSourceDelivery>
{
    public void Configure(EntityTypeBuilder<UserJobSourceDelivery> builder)
    {
        builder.ToTable("UserJobSourceDeliveries");
        builder.HasKey(d => new { d.UserId, d.PostingId });

        builder.HasIndex(d => new { d.UserId, d.WeekKey, d.Rank });
        builder.HasIndex(d => new { d.UserId, d.DeliveredAt });
        builder.HasIndex(d => d.PostingId);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        // A delivered posting is never pruned; the sweep filters them out and the database refuses
        // the rest.
        builder.HasOne<JobSourcePosting>().WithMany().HasForeignKey(d => d.PostingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JobSourceQuery>().WithMany().HasForeignKey(d => d.QueryId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserJobSourceRunConfiguration : IEntityTypeConfiguration<UserJobSourceRun>
{
    public void Configure(EntityTypeBuilder<UserJobSourceRun> builder)
    {
        builder.ToTable("UserJobSourceRuns");
        builder.HasKey(r => new { r.UserId, r.WeekKey });

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
