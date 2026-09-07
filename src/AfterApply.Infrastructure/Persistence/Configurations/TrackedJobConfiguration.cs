using AfterApply.Domain.Companies;
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

        builder.HasIndex(t => t.UserId);

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
    }
}
