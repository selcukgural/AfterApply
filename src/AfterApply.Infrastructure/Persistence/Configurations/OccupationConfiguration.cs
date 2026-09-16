using AfterApply.Domain.Occupations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class OccupationConfiguration : IEntityTypeConfiguration<Occupation>
{
    public void Configure(EntityTypeBuilder<Occupation> builder)
    {
        builder.ToTable("Occupations");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Code).HasMaxLength(Occupation.MaxCodeLength).IsRequired();
        builder.Property(o => o.Isco08Code).HasMaxLength(Occupation.Isco08CodeLength).IsRequired();
        builder.Property(o => o.NameTr).HasMaxLength(Occupation.MaxNameLength).IsRequired();
        builder.Property(o => o.NameEn).HasMaxLength(Occupation.MaxNameLength).IsRequired();
        builder.Property(o => o.NormalizedNameTr).HasMaxLength(Occupation.MaxNameLength).IsRequired();
        builder.Property(o => o.NormalizedNameEn).HasMaxLength(Occupation.MaxNameLength).IsRequired();
        builder.Property(o => o.Source).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.IsActive).HasDefaultValue(true);

        // The seed upserts on this; it is also what a later CSV version matches rows by.
        builder.HasIndex(o => o.Code).IsUnique();
        builder.HasIndex(o => o.Isco08Code);
        // The two trigram GIN indexes the search runs on are raw SQL in the AddOccupations migration
        // (gin_trgm_ops has no fluent equivalent) — see AddCompanyNameTrigramIndex for the idiom.
    }
}
