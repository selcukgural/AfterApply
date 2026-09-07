using AfterApply.Domain.Feedback;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class FeedbackEntryConfiguration : IEntityTypeConfiguration<FeedbackEntry>
{
    public void Configure(EntityTypeBuilder<FeedbackEntry> builder)
    {
        builder.ToTable("FeedbackEntries");
        builder.HasKey(f => f.Id);

        // Same width as SubmitFeedbackRequestValidator.MaxMessageLength and the panel's counter —
        // a bounded column rather than text, so the endpoint can't be used as free storage.
        builder.Property(f => f.Message).IsRequired().HasMaxLength(1000);
        builder.Property(f => f.ReplyEmail).HasMaxLength(320);
        builder.Property(f => f.PagePath).HasMaxLength(200);
        builder.Property(f => f.Locale).HasMaxLength(10);
        builder.Property(f => f.Theme).HasMaxLength(10);
        builder.Property(f => f.UserAgent).HasMaxLength(400);
        builder.Property(f => f.AdminReply).HasColumnType("text");
        builder.Property(f => f.GitHubIssueUrl).HasMaxLength(500);

        // Stored as text: the "your feedback" screen this table is being modelled for reads these
        // by name, and a renumbered enum silently rewriting history is not a trade worth the bytes.
        builder.Property(f => f.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Mood).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // The planned per-user list ("what happened to what I sent?") reads exactly this order.
        builder.HasIndex(f => new { f.UserId, f.SubmittedAt });

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
