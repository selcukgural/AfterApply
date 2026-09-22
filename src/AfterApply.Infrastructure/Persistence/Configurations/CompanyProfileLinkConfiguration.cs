using AfterApply.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyProfileLinkConfiguration : IEntityTypeConfiguration<CompanyProfileLink>
{
    public void Configure(EntityTypeBuilder<CompanyProfileLink> builder)
    {
        builder.ToTable("CompanyProfileLinks");
        builder.HasKey(l => l.Id);

        // Same string conversion as Job.Source/Application.Source, so a link's platform and a
        // posting's source read identically in the database.
        builder.Property(l => l.Platform).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(l => l.Url).IsRequired().HasMaxLength(500);

        // One link per platform per company — the fill-if-missing rule in
        // Company.AddProfileLinkIfMissing is the first line of defence, this index is the one that
        // holds when two requests capture the same company at once.
        builder.HasIndex(l => new { l.CompanyId, l.Platform }).IsUnique();

        builder.HasOne<Company>()
            .WithMany(c => c.ProfileLinks)
            .HasForeignKey(l => l.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
