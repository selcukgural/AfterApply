using AfterApply.Domain.Companies;
using AfterApply.Domain.Jobs;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class TrackedJobConfiguration : IEntityTypeConfiguration<TrackedJob>
{
    public void Configure(EntityTypeBuilder<TrackedJob> builder)
    {
        builder.ToTable("TrackedJobs");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.JobTitle).IsRequired().HasMaxLength(300);
        builder.Property(t => t.JobUrl).HasMaxLength(2000);
        builder.Property(t => t.Location).HasMaxLength(200);
        builder.Property(t => t.Notes).HasColumnType("text");
        builder.Property(t => t.HrName).HasMaxLength(200);
        builder.Property(t => t.HrEmail).HasMaxLength(320);
        builder.Property(t => t.HrLinkedInUrl).HasMaxLength(500);
        builder.Property(t => t.CapturedJobDescriptionHtml).HasColumnType("text");

        builder.HasIndex(t => t.UserId);
        // The extension's "save for later" and "I Applied" both look a posting up by its URL within
        // one user's rows (dedup, and turning a saved posting into an application).
        builder.HasIndex(t => new { t.UserId, t.JobUrl });

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // SetNull like Application.JobId: the shared Job row is not this user's to keep alive.
        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(t => t.JobId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
