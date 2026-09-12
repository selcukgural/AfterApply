using AfterApply.Domain.JobSearch;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSearchUsageConfiguration : IEntityTypeConfiguration<JobSearchUsage>
{
    public void Configure(EntityTypeBuilder<JobSearchUsage> builder)
    {
        builder.ToTable("JobSearchUsages");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Operation).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(u => u.UpstreamRequestId).HasMaxLength(64);

        // The per-user daily sum and the global monthly sum, respectively.
        builder.HasIndex(u => new { u.UserId, u.RequestedAt });
        builder.HasIndex(u => u.RequestedAt);

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
