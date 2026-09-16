using AfterApply.Domain.CompanyReviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyReviewCategoryRatingConfiguration : IEntityTypeConfiguration<CompanyReviewCategoryRating>
{
    public void Configure(EntityTypeBuilder<CompanyReviewCategoryRating> builder)
    {
        builder.ToTable("CompanyReviewCategoryRatings");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(30).IsRequired();

        // One rating per category per review; also the lookup the projections use.
        builder.HasIndex(x => new { x.ReviewId, x.Category }).IsUnique();

        builder.HasOne<CompanyReview>()
            .WithMany()
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
