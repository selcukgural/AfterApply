using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class HelpfulNotificationLedgerEntryConfiguration : IEntityTypeConfiguration<HelpfulNotificationLedgerEntry>
{
    public void Configure(EntityTypeBuilder<HelpfulNotificationLedgerEntry> builder)
    {
        builder.ToTable("HelpfulNotificationLedger");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(40);

        // ContributionNotificationWriter's ON CONFLICT DO NOTHING target — change both together.
        builder.HasIndex(e => new { e.Type, e.TargetId, e.VoterUserId }).IsUnique();
        builder.HasIndex(e => e.CountedAt);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.VoterUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
