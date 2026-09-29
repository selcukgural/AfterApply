using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("Users");
        builder.Property(u => u.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(u => u.LastName).IsRequired().HasMaxLength(100);

        // Profile photo (DECISIONS.md 2026-09-28). The public id is what a URL carries, so it is
        // looked up on every image request — and it must never collide with another account's.
        builder.Property(u => u.AvatarObjectName).HasMaxLength(200);
        builder.HasIndex(u => u.AvatarPublicId).IsUnique();
    }
}
