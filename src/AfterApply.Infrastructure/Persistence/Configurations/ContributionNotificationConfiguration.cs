using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ContributionNotificationConfiguration : IEntityTypeConfiguration<ContributionNotification>
{
    public void Configure(EntityTypeBuilder<ContributionNotification> builder)
    {
        builder.ToTable("ContributionNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(40);

        // The upsert's conflict target: one row per contribution per day. ContributionNotificationWriter
        // names these columns in its ON CONFLICT clause — change both together.
        builder.HasIndex(n => new { n.UserId, n.Type, n.TargetId, n.Day }).IsUnique();
        builder.HasIndex(n => new { n.UserId, n.LastEventAt });

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
