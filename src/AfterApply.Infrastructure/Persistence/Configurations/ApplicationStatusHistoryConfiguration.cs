using AfterApply.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistories");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Note).HasMaxLength(500);
        builder.Property(h => h.Origin).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.RejectionReasonCategory).HasConversion<string>().HasMaxLength(50);
        builder.Property(h => h.RejectionReasonDetail).HasMaxLength(500);

        builder.HasIndex(h => h.ApplicationId);

        // The status history screen is always "this application, newest first" — the covering
        // composite keeps that from degrading into a sort over the per-application index.
        builder.HasIndex(h => new { h.ApplicationId, h.ChangedAt });
    }
}
