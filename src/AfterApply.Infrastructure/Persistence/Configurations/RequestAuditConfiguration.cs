using AfterApply.Domain.Auditing;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class RequestAuditConfiguration : IEntityTypeConfiguration<RequestAudit>
{
    public void Configure(EntityTypeBuilder<RequestAudit> builder)
    {
        builder.ToTable("RequestAudits");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Method).IsRequired().HasMaxLength(RequestAudit.MaxMethodLength);
        builder.Property(a => a.Path).IsRequired().HasMaxLength(RequestAudit.MaxPathLength);
        builder.Property(a => a.IpAddress).HasMaxLength(RequestAudit.MaxIpAddressLength);

        // "Everything this account did" for a legal request; "everything older than the window"
        // for the anonymous-row purge.
        builder.HasIndex(a => new { a.UserId, a.At });
        builder.HasIndex(a => a.At);

        // Nullable on purpose — an anonymous request has no account — but a row that names a user
        // must name a real one, and it leaves with the account like every other user-owned row
        // (see ApplicationConfiguration for the cascade-from-Users rule). The anonymous rows are
        // the one kind this cascade cannot reach; RequestAuditRetentionService ages them out.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
