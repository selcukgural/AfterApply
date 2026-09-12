using AfterApply.Domain.JobSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSearchJobConfiguration : IEntityTypeConfiguration<JobSearchJob>
{
    public void Configure(EntityTypeBuilder<JobSearchJob> builder)
    {
        builder.ToTable("JobSearchJobs");
        builder.HasKey(j => j.Id);

        // The provider's v5 ids are composite tokens of ~400 characters, not the 24-character
        // samples its documentation shows — measured 402 on the first real call (2026-09-12).
        builder.Property(j => j.JobId).IsRequired().HasMaxLength(JobSearchJob.MaxJobIdLength);
        builder.Property(j => j.Country).IsRequired().HasMaxLength(2);
        builder.Property(j => j.Title).HasMaxLength(300);
        builder.Property(j => j.EmployerName).HasMaxLength(200);
        builder.Property(j => j.Publisher).HasMaxLength(100);
        builder.Property(j => j.Summary).IsRequired().HasColumnType("jsonb");
        builder.Property(j => j.Detail).HasColumnType("jsonb");

        // One row per posting per country — the details lookup is by this pair, id by id.
        builder.HasIndex(j => new { j.JobId, j.Country }).IsUnique();
        builder.HasIndex(j => j.EmployerName);

        // No foreign key to Users, deliberately: a posting is public data and the row records
        // nothing about who looked at it. See the entity's doc comment.
    }
}
