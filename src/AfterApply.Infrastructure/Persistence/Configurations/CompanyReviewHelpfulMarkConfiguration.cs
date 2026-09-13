using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyReviewHelpfulMarkConfiguration : IEntityTypeConfiguration<CompanyReviewHelpfulMark>
{
    public void Configure(EntityTypeBuilder<CompanyReviewHelpfulMark> builder)
    {
        builder.ToTable("CompanyReviewHelpfulMarks");
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.ReviewId, m.UserId }).IsUnique();

        builder.HasOne<CompanyReview>()
            .WithMany()
            .HasForeignKey(m => m.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
