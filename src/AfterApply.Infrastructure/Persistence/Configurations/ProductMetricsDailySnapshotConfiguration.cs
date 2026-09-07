using AfterApply.Domain.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ProductMetricsDailySnapshotConfiguration : IEntityTypeConfiguration<ProductMetricsDailySnapshot>
{
    public void Configure(EntityTypeBuilder<ProductMetricsDailySnapshot> builder)
    {
        builder.ToTable("ProductMetricsDailySnapshots");
        builder.HasKey(s => s.Id);

        // The upsert key. Unique so a second run of the same day can only ever update the existing
        // row — the database enforces what ComputeSnapshotAsync intends, rather than trusting it.
        builder.HasIndex(s => s.SnapshotDate).IsUnique();

        // No foreign key to Users on purpose: this table holds product-wide aggregates, so it is
        // outside the cascade-from-Users rule that every user-owned table follows
        // (DECISIONS.md 2026-09-07). Nothing is missing here.
    }
}
