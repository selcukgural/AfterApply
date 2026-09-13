using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CompanyReviewReportConfiguration : IEntityTypeConfiguration<CompanyReviewReport>
{
    public void Configure(EntityTypeBuilder<CompanyReviewReport> builder)
    {
        builder.ToTable("CompanyReviewReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Note).HasMaxLength(CompanyReviewReport.MaxNoteLength);
        builder.Property(r => r.ResolutionReason).HasMaxLength(CompanyReviewReport.MaxResolutionReasonLength);
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Resolution).HasConversion<string>().HasMaxLength(30);

        // One *open* report per reader per review. Partial, so the same reader can report again
        // after an admin dismissed the first one and the text changed — see JobConfiguration for
        // the filtered-index idiom.
        builder.HasIndex(r => new { r.ReviewId, r.ReporterUserId })
            .IsUnique()
            .HasFilter("\"Status\" = 'Open'");
        builder.HasIndex(r => new { r.Status, r.ReportedAt });

        builder.HasOne<CompanyReview>()
            .WithMany()
            .HasForeignKey(r => r.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);

        // The reporter's account going away takes the report with it: a report is that person's
        // statement, and the cascade-from-Users rule applies to it like any other user-owned row.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
