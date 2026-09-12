using AfterApply.Domain.JobSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSourcePostingConfiguration : IEntityTypeConfiguration<JobSourcePosting>
{
    public void Configure(EntityTypeBuilder<JobSourcePosting> builder)
    {
        builder.ToTable("JobSourcePostings");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.ExternalId).IsRequired().HasMaxLength(JobSourcePosting.MaxExternalIdLength);
        builder.Property(p => p.Title).IsRequired().HasMaxLength(300);
        builder.Property(p => p.CompanyName).IsRequired().HasMaxLength(300);
        builder.Property(p => p.CompanyProfileUrl).HasMaxLength(500);
        builder.Property(p => p.Location).HasMaxLength(200);
        builder.Property(p => p.Url).IsRequired().HasMaxLength(500);
        builder.Property(p => p.Description).HasColumnType("text");
        builder.Property(p => p.Seniority).HasMaxLength(100);
        builder.Property(p => p.EmploymentType).HasMaxLength(100);
        builder.Property(p => p.JobFunction).HasMaxLength(200);
        builder.Property(p => p.Industries).HasMaxLength(500);

        builder.HasIndex(p => new { p.Source, p.ExternalId }).IsUnique();
        builder.HasIndex(p => p.LastSeenAt);
    }
}
