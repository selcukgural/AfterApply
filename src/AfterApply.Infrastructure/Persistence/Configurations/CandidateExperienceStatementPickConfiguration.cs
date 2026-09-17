using AfterApply.Domain.CandidateExperiences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceStatementPickConfiguration : IEntityTypeConfiguration<CandidateExperienceStatementPick>
{
    public void Configure(EntityTypeBuilder<CandidateExperienceStatementPick> builder)
    {
        builder.ToTable("CandidateExperienceStatementPicks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StatementKey).IsRequired().HasMaxLength(CandidateExperienceStatementPick.MaxKeyLength);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.ExperienceId, x.StatementKey }).IsUnique();
        // The "most picked" lists group by key across a company's entries.
        builder.HasIndex(x => x.StatementKey);

        builder.HasOne<CandidateExperience>()
            .WithMany()
            .HasForeignKey(x => x.ExperienceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
