using AfterApply.Domain.Ai;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class AiUsageEntryConfiguration : IEntityTypeConfiguration<AiUsageEntry>
{
    public void Configure(EntityTypeBuilder<AiUsageEntry> builder)
    {
        builder.ToTable("AiUsageEntries");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Feature).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(e => e.Model).IsRequired().HasMaxLength(AiUsageEntry.MaxModelLength);

        // The budget reads "this feature, since the start of the day/month".
        builder.HasIndex(e => new { e.Feature, e.At });

        // A cost record outlives the account: the month's total must not shrink because a user
        // left. The row holds nothing the user wrote — no prompt, no output, only counts — which is
        // what makes this the one user-linked table that sets null instead of cascading (compare
        // ApplicationConfiguration). A row that names a user still has to name a real one.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
