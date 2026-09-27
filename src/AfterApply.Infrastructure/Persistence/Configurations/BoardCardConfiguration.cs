using AfterApply.Domain.Board;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BoardCardConfiguration : IEntityTypeConfiguration<BoardCard>
{
    public void Configure(EntityTypeBuilder<BoardCard> builder)
    {
        builder.ToTable("BoardCards", table => table.HasCheckConstraint(
            "CK_BoardCards_ExactlyOneItem",
            "(\"ApplicationId\" IS NULL) <> (\"TrackedJobId\" IS NULL)"));
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Origin).HasConversion<string>().HasMaxLength(50);

        // One card per item: "add to board" and the automatic paths insert with ON CONFLICT on
        // these, so a double click or two tabs cannot put the same application up twice.
        builder.HasIndex(c => c.ApplicationId).IsUnique().HasFilter("\"ApplicationId\" IS NOT NULL");
        builder.HasIndex(c => c.TrackedJobId).IsUnique().HasFilter("\"TrackedJobId\" IS NOT NULL");

        // The column pages walk (UserId, Position, Id) — see BoardCursor.
        builder.HasIndex(c => new { c.UserId, c.Position, c.Id });
        // The nightly purge looks for closed cards past their window, across every user.
        builder.HasIndex(c => c.ClosedAt).HasFilter("\"ClosedAt\" IS NOT NULL");

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A deleted application or saved posting takes its card with it. Converting a saved posting
        // moves the card onto the new application first (BoardCard.BecomeApplication), in the same
        // SaveChanges that deletes the posting.
        builder.HasOne<DomainApplication>()
            .WithMany()
            .HasForeignKey(c => c.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<TrackedJob>()
            .WithMany()
            .HasForeignKey(c => c.TrackedJobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class BoardStateConfiguration : IEntityTypeConfiguration<BoardState>
{
    public void Configure(EntityTypeBuilder<BoardState> builder)
    {
        builder.ToTable("BoardStates");
        builder.HasKey(s => s.UserId);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
