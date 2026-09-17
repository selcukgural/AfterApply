using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CandidateExperienceConfiguration : IEntityTypeConfiguration<CandidateExperience>
{
    public void Configure(EntityTypeBuilder<CandidateExperience> builder)
    {
        builder.ToTable("CandidateExperiences");
        builder.HasKey(e => e.Id);

        // Strings, as every enum column in this schema (see CompanyReviewConfiguration).
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Duration).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Stages).HasConversion<string>().HasMaxLength(10);

        // One experience per company per account — enforced where it cannot be raced.
        builder.HasIndex(e => new { e.UserId, e.CompanyId }).IsUnique();
        // The company's list, newest first; also the count on the public company page.
        builder.HasIndex(e => new { e.CompanyId, e.SubmittedAt });
        // The quota count and the author's own list.
        builder.HasIndex(e => e.UserId);

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
