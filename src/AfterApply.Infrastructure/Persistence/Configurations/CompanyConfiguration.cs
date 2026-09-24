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
        builder.Property(c => c.WebsiteSource).HasConversion<string>().HasMaxLength(32);
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

public sealed class CompanyProfileSubmissionConfiguration : IEntityTypeConfiguration<CompanyProfileSubmission>
{
    public void Configure(EntityTypeBuilder<CompanyProfileSubmission> builder)
    {
        builder.ToTable("CompanyProfileSubmissions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Platform).HasConversion<string>().HasMaxLength(32);
        builder.Property(s => s.Url).IsRequired().HasMaxLength(500);

        builder.HasIndex(s => new { s.CompanyId, s.Platform, s.UserId }).IsUnique();

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(s => s.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user's word leaves with the account, like everything else they own.
        builder.HasOne<AfterApply.Infrastructure.Identity.ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
