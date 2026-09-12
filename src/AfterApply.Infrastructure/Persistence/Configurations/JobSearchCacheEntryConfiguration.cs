using AfterApply.Domain.JobSearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSearchCacheEntryConfiguration : IEntityTypeConfiguration<JobSearchCacheEntry>
{
    public void Configure(EntityTypeBuilder<JobSearchCacheEntry> builder)
    {
        builder.ToTable("JobSearchCacheEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Operation).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.KeyHash).IsRequired().HasMaxLength(64);
        builder.Property(e => e.Parameters).HasColumnType("text");
        builder.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.UpstreamRequestId).HasMaxLength(64);

        builder.HasIndex(e => new { e.Operation, e.KeyHash }).IsUnique();
        // The lazy sweep in JobSearchService.StoreAsync deletes by this.
        builder.HasIndex(e => e.ExpiresAt);

        // No foreign key to Users — the row holds a normalised request and its answer, never
        // who asked. See the entity's doc comment.
    }
}
