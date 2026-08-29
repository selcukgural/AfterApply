using AfterApply.Domain.Companies;
using AfterApply.Domain.TrackedJobs;
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

        builder.HasIndex(t => t.UserId);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(t => t.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
