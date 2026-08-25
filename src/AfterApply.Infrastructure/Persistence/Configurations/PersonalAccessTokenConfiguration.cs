using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class PersonalAccessTokenConfiguration : IEntityTypeConfiguration<PersonalAccessToken>
{
    public void Configure(EntityTypeBuilder<PersonalAccessToken> builder)
    {
        builder.ToTable("PersonalAccessTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.TokenHash).IsRequired().HasMaxLength(128);

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.UserId, t.RevokedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
