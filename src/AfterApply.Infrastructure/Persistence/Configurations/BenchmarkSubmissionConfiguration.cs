using AfterApply.Domain.Benchmark;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BenchmarkSubmissionConfiguration : IEntityTypeConfiguration<BenchmarkSubmission>
{
    public void Configure(EntityTypeBuilder<BenchmarkSubmission> builder)
    {
        builder.ToTable("BenchmarkSubmissions");
        builder.HasKey(s => s.Id);

        // Text, like every other enum in this schema: a new sector is a code change rather than a
        // migration, and a row stays readable in psql.
        builder.Property(s => s.Sector).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(s => s.Period).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.Seniority).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Location).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Locale).HasMaxLength(8).IsRequired();

        // The comparison cell is the sector, and it is read on every submission.
        builder.HasIndex(s => s.Sector);

        // ReplyRate is computed from two stored columns; nothing to map.
        builder.Ignore(s => s.ReplyRate);

        // No foreign key to Users, because there is no UserId — see BenchmarkSubmission's summary
        // for why that is deliberate. This table is outside the cascade-from-Users rule that every
        // user-owned table follows (DECISIONS.md 2026-09-07); nothing is missing here.
    }
}
