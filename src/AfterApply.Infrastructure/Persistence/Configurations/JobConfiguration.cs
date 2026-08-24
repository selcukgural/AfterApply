using AfterApply.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Title).IsRequired().HasMaxLength(300);
        builder.Property(j => j.NormalizedTitle).IsRequired().HasMaxLength(300);
        builder.Property(j => j.Url).HasMaxLength(2000);
        builder.Property(j => j.ExternalId).HasMaxLength(200);
        builder.Property(j => j.Location).HasMaxLength(200);
        builder.Property(j => j.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(j => j.RemoteType).HasConversion<string>().HasMaxLength(50);
        builder.Property(j => j.EmploymentType).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(j => j.CompanyId);
        builder.HasIndex(j => new { j.Source, j.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");
    }
}
