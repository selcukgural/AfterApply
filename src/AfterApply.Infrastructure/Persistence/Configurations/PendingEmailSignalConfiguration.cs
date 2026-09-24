using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class PendingEmailSignalConfiguration : IEntityTypeConfiguration<PendingEmailSignal>
{
    public void Configure(EntityTypeBuilder<PendingEmailSignal> builder)
    {
        builder.ToTable("PendingEmailSignals");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Payload).IsRequired().HasColumnType("jsonb");

        // The purge's range scan.
        builder.HasIndex(s => s.CreatedAt);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
