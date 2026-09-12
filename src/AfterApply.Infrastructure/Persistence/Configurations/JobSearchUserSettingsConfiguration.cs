using AfterApply.Domain.JobSearch;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class JobSearchUserSettingsConfiguration : IEntityTypeConfiguration<JobSearchUserSettings>
{
    public void Configure(EntityTypeBuilder<JobSearchUserSettings> builder)
    {
        builder.ToTable("JobSearchUserSettings");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.DefaultCountry).HasMaxLength(2);
        builder.Property(s => s.DefaultLanguage).HasMaxLength(3);
        builder.Property(s => s.DefaultLocation).HasMaxLength(200);
        builder.Property(s => s.DefaultDatePosted).HasMaxLength(16);

        // One settings row per account.
        builder.HasIndex(s => s.UserId).IsUnique();

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
