using AfterApply.Domain.Pro;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ProEntitlementConfiguration : IEntityTypeConfiguration<ProEntitlement>
{
    public void Configure(EntityTypeBuilder<ProEntitlement> builder)
    {
        builder.ToTable("ProEntitlements");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(e => e.UserId).IsUnique();

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
