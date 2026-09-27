using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class InterviewNotificationConfiguration : IEntityTypeConfiguration<InterviewNotification>
{
    public void Configure(EntityTypeBuilder<InterviewNotification> builder)
    {
        builder.ToTable("InterviewNotifications");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(20);

        // One row per interview per kind: the scan runs nightly and must not ring twice.
        builder.HasIndex(n => new { n.ApplicationId, n.Kind, n.InterviewAt }).IsUnique();
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });

        // Cascade from the account and from the application — see ApplicationConfiguration for why
        // these are foreign keys and not lines in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<DomainApplication>()
            .WithMany()
            .HasForeignKey(n => n.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
