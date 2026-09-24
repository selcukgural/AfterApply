using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Companies;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ExperienceInviteDismissalConfiguration : IEntityTypeConfiguration<ExperienceInviteDismissal>
{
    public void Configure(EntityTypeBuilder<ExperienceInviteDismissal> builder)
    {
        builder.ToTable("ExperienceInviteDismissals");
        builder.HasKey(d => d.Id);

        // Dismissing twice is a no-op, not a second row — ExperienceInviteService relies on it.
        builder.HasIndex(d => new { d.UserId, d.CompanyId }).IsUnique();

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
