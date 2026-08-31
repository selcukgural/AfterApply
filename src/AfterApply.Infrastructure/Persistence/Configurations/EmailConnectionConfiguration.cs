using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class EmailConnectionConfiguration : IEntityTypeConfiguration<EmailConnection>
{
    public void Configure(EntityTypeBuilder<EmailConnection> builder)
    {
        builder.ToTable("EmailConnections");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.ProviderAccountEmail).IsRequired().HasMaxLength(256);
        builder.Property(c => c.GrantedScopes).IsRequired().HasMaxLength(500);
        builder.Property(c => c.LastSyncError).HasMaxLength(1000);
        builder.Property(c => c.InboundToken).HasMaxLength(32);

        builder.HasIndex(c => new { c.UserId, c.Provider }).IsUnique();

        // Nullable — Postgres treats multiple NULLs as distinct, so Gmail-provider rows (which never
        // set this) don't collide with each other; only real tokens are enforced unique.
        builder.HasIndex(c => c.InboundToken).IsUnique();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
