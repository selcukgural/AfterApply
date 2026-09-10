using AfterApply.Domain.CvScan;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CvScanResultConfiguration : IEntityTypeConfiguration<CvScanResult>
{
    public void Configure(EntityTypeBuilder<CvScanResult> builder)
    {
        builder.ToTable("CvScanResults");
        builder.HasKey(result => result.Id);

        builder.Property(result => result.Format).HasConversion<string>().HasMaxLength(16).IsRequired();

        // The distribution is read by score and the stopping condition is counted by date; both
        // are cheap enough on this shape that one index over the pair covers them. The same index
        // serves layer B's daily ceiling, which counts today's rows by date and then filters a
        // boolean — a filter cheap enough not to earn a column in the key.
        builder.HasIndex(result => new { result.ScannedAt, result.Score });

        // No foreign key to Users — see CvScanResult's summary. Deliberate, not an omission.
    }
}
