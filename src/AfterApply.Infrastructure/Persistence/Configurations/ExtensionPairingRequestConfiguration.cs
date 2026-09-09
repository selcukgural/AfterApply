using AfterApply.Application.Identity;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ExtensionPairingRequestConfiguration : IEntityTypeConfiguration<ExtensionPairingRequest>
{
    public void Configure(EntityTypeBuilder<ExtensionPairingRequest> builder)
    {
        builder.ToTable("ExtensionPairingRequests");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Code).IsRequired().HasMaxLength(ExtensionPairingCode.Length);
        builder.Property(r => r.DeviceSecretHash).IsRequired().HasMaxLength(128);

        // Both lookups in the flow are single-row by one of these two columns, and both must be
        // unique: a duplicate code would point two extensions at one confirmation, a duplicate
        // secret hash would make "which pairing is this poll for" ambiguous.
        builder.HasIndex(r => r.Code).IsUnique();
        builder.HasIndex(r => r.DeviceSecretHash).IsUnique();

        // The sweep in ExtensionPairingService.StartAsync scans by expiry on every start.
        builder.HasIndex(r => r.ExpiresAt);

        // Nullable FK, cascade like every other user-owned row (DECISIONS.md 2026-09-07): a pending
        // request belongs to nobody, an approved one belongs to the account that approved it and
        // must not outlive it.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
