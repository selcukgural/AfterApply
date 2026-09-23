using AfterApply.Domain.CandidateExperiences;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceHelpfulMarkConfiguration : IEntityTypeConfiguration<CandidateExperienceHelpfulMark>
{
    public void Configure(EntityTypeBuilder<CandidateExperienceHelpfulMark> builder)
    {
        builder.ToTable("CandidateExperienceHelpfulMarks");
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.ExperienceId, m.UserId }).IsUnique();

        builder.HasOne<CandidateExperience>()
            .WithMany()
            .HasForeignKey(m => m.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
