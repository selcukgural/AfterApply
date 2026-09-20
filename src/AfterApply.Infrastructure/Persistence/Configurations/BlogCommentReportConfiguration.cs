using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogCommentReportConfiguration : IEntityTypeConfiguration<BlogCommentReport>
{
    public void Configure(EntityTypeBuilder<BlogCommentReport> builder)
    {
        builder.ToTable("BlogCommentReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Note).HasMaxLength(BlogCommentReport.MaxNoteLength);

        // One report per reader per comment; the admin's "reported" filter reads the open ones.
        builder.HasIndex(r => new { r.CommentId, r.ReporterUserId }).IsUnique();
        builder.HasIndex(r => new { r.CommentId, r.Status });

        builder.HasOne<BlogComment>()
            .WithMany()
            .HasForeignKey(r => r.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
