using AfterApply.Domain.JobSources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSourceQueryConfiguration : IEntityTypeConfiguration<JobSourceQuery>
{
    public void Configure(EntityTypeBuilder<JobSourceQuery> builder)
    {
        builder.ToTable("JobSourceQueries");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(q => q.Keywords).IsRequired().HasMaxLength(JobSourceQuery.MaxKeywordsLength);
        builder.Property(q => q.Location).IsRequired().HasMaxLength(JobSourceQuery.MaxLocationLength);
        builder.Property(q => q.TimeWindow).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(q => q.LastOutcome).HasMaxLength(50);

        builder.HasIndex(q => q.KeyHash).IsUnique();
    }
}

public sealed class JobSourceQueryPostingConfiguration : IEntityTypeConfiguration<JobSourceQueryPosting>
{
    public void Configure(EntityTypeBuilder<JobSourceQueryPosting> builder)
    {
        builder.ToTable("JobSourceQueryPostings");
        builder.HasKey(l => new { l.QueryId, l.PostingId });

        builder.HasOne<JobSourceQuery>().WithMany().HasForeignKey(l => l.QueryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<JobSourcePosting>().WithMany().HasForeignKey(l => l.PostingId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.QueryId, l.LastSeenAt });
    }
}

public sealed class JobSourceFetchConfiguration : IEntityTypeConfiguration<JobSourceFetch>
{
    public void Configure(EntityTypeBuilder<JobSourceFetch> builder)
    {
        builder.ToTable("JobSourceFetches");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Outcome).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(f => f.At);
    }
}
