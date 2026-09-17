using AfterApply.Domain.CandidateExperiences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceInterviewTypeConfiguration : IEntityTypeConfiguration<CandidateExperienceInterviewType>
{
    public void Configure(EntityTypeBuilder<CandidateExperienceInterviewType> builder)
    {
        builder.ToTable("CandidateExperienceInterviewTypes");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(x => new { x.ExperienceId, x.Type }).IsUnique();
        // The company page counts each type across a company's entries.
        builder.HasIndex(x => x.Type);

        builder.HasOne<CandidateExperience>()
            .WithMany()
            .HasForeignKey(x => x.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
