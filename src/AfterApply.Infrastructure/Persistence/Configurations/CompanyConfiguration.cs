using AfterApply.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(300);
        builder.Property(c => c.NormalizedName).IsRequired().HasMaxLength(300);
        builder.Property(c => c.Website).HasMaxLength(500);
        builder.Property(c => c.LinkedInUrl).HasMaxLength(500);
        builder.Property(c => c.KariyerNetUrl).HasMaxLength(500);
        builder.Property(c => c.Industry).HasMaxLength(200);
        builder.Property(c => c.Country).HasMaxLength(2);

        builder.HasIndex(c => c.NormalizedName).IsUnique();

        // Nullable + unique: Postgres treats NULLs as distinct, so the rollout window in which an
        // old instance inserts a company without a slug (see Company.Slug) never trips the index.
        builder.Property(c => c.Slug).HasMaxLength(100);
        builder.HasIndex(c => c.Slug).IsUnique();
    }
}
