using AfterApply.Domain.CandidateExperiences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceCategoryRatingConfiguration : IEntityTypeConfiguration<CandidateExperienceCategoryRating>
{
    public void Configure(EntityTypeBuilder<CandidateExperienceCategoryRating> builder)
    {
        builder.ToTable("CandidateExperienceCategoryRatings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(x => new { x.ExperienceId, x.Category }).IsUnique();

        // No navigation collection on the parent on purpose — see the 2026-09-13 EF Include note
        // in DECISIONS.md; the service loads children by id.
        builder.HasOne<CandidateExperience>()
            .WithMany()
            .HasForeignKey(x => x.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
