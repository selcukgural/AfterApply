using AfterApply.Domain.CompanyReviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyReviewStatementPickConfiguration : IEntityTypeConfiguration<CompanyReviewStatementPick>
{
    public void Configure(EntityTypeBuilder<CompanyReviewStatementPick> builder)
    {
        builder.ToTable("CompanyReviewStatementPicks");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StatementKey).IsRequired().HasMaxLength(CompanyReviewStatementPick.MaxKeyLength);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.HasIndex(x => new { x.ReviewId, x.StatementKey }).IsUnique();
        // The "most picked" lists group by key across a company's approved reviews.
        builder.HasIndex(x => x.StatementKey);

        builder.HasOne<CompanyReview>()
            .WithMany()
            .HasForeignKey(x => x.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
