using AfterApply.Domain.FeatureFlags;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class FeatureFlagOverrideConfiguration : IEntityTypeConfiguration<FeatureFlagOverride>
{
    public const int KeyLength = 64;

    public void Configure(EntityTypeBuilder<FeatureFlagOverride> builder)
    {
        builder.ToTable("FeatureFlagOverrides");
        builder.HasKey(o => o.Key);
        builder.Property(o => o.Key).HasMaxLength(KeyLength);
        // The concurrency token: two admins confirming over the same override cannot both win —
        // the later UPDATE/DELETE carries the UpdatedAt it read and matches nothing.
        builder.Property(o => o.UpdatedAt).IsConcurrencyToken();

        // The switch outlives the admin who set it: deleting that account keeps the flag where it
        // is and forgets only who.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UpdatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class FeatureFlagChangeConfiguration : IEntityTypeConfiguration<FeatureFlagChange>
{
    public void Configure(EntityTypeBuilder<FeatureFlagChange> builder)
    {
        builder.ToTable("FeatureFlagChanges");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Key).HasMaxLength(FeatureFlagOverrideConfiguration.KeyLength);
        builder.Property(c => c.Reason).HasMaxLength(FeatureFlagChange.MaxReasonLength).IsRequired();
        builder.HasIndex(c => c.ChangedAt);
        builder.HasIndex(c => new { c.Key, c.ChangedAt });

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class FeatureFlagChangeOriginConfiguration : IEntityTypeConfiguration<FeatureFlagChangeOrigin>
{
    public void Configure(EntityTypeBuilder<FeatureFlagChangeOrigin> builder)
    {
        builder.ToTable("FeatureFlagChangeOrigins");
        builder.HasKey(o => o.ChangeId);
        builder.Property(o => o.IpAddress).HasMaxLength(FeatureFlagChangeOrigin.MaxIpAddressLength);
        builder.HasIndex(o => o.UserId);

        builder.HasOne<FeatureFlagChange>()
            .WithOne()
            .HasForeignKey<FeatureFlagChangeOrigin>(o => o.ChangeId)
            .OnDelete(DeleteBehavior.Cascade);

        // The account goes, its connections go (the RequestAudits rule); the change it made stays.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
