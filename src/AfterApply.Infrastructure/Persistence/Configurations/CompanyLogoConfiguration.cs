using AfterApply.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyLogoConfiguration : IEntityTypeConfiguration<CompanyLogo>
{
    public void Configure(EntityTypeBuilder<CompanyLogo> builder)
    {
        // Its own table rather than columns on Companies: every company read would otherwise drag
        // tens of kilobytes along, and most reads never draw a logo.
        builder.ToTable("CompanyLogos");
        builder.HasKey(l => l.CompanyId);
        builder.Property(l => l.ContentType).HasMaxLength(50);

        builder.HasOne<Company>()
            .WithOne()
            .HasForeignKey<CompanyLogo>(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
