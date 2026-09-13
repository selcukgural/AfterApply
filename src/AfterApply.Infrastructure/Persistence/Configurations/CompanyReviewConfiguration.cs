using AfterApply.Domain.Companies;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyReviewConfiguration : IEntityTypeConfiguration<CompanyReview>
{
    public void Configure(EntityTypeBuilder<CompanyReview> builder)
    {
        builder.ToTable("CompanyReviews");
        builder.HasKey(r => r.Id);

        // Same widths as the validator and the form's counters — bounded columns, not text.
        builder.Property(r => r.Title).IsRequired().HasMaxLength(CompanyReview.MaxTitleLength);
        builder.Property(r => r.Pros).IsRequired().HasMaxLength(CompanyReview.MaxTextLength);
        builder.Property(r => r.Cons).IsRequired().HasMaxLength(CompanyReview.MaxTextLength);
        builder.Property(r => r.RejectionReason).HasMaxLength(CompanyReview.MaxRejectionReasonLength);

        // Strings, as every enum column in this schema: the admin queue and the author's list read
        // these by name, and a renumbered enum silently rewriting history is not worth the bytes.
        builder.Property(r => r.EmploymentStatus).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // One voice per company per account — the rule the score depends on, enforced where it
        // cannot be raced.
        builder.HasIndex(r => new { r.UserId, r.CompanyId }).IsUnique();
        // The public page: approved reviews of one company, newest first; also the aggregate.
        builder.HasIndex(r => new { r.CompanyId, r.Status, r.SubmittedAt });
        // The moderation queue.
        builder.HasIndex(r => new { r.Status, r.SubmittedAt });

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(r => r.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
