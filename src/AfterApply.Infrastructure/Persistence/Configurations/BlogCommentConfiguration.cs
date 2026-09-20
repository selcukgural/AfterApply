using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogCommentConfiguration : IEntityTypeConfiguration<BlogComment>
{
    public void Configure(EntityTypeBuilder<BlogComment> builder)
    {
        builder.ToTable("BlogComments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content).HasMaxLength(BlogComment.MaxContentLength);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        // The page's query: a post's approved comments, newest first — and the admin's: pending
        // ones across posts, which the same index serves through its Status column.
        builder.HasIndex(c => new { c.PostId, c.Status, c.CreatedAt });
        // The reader's own list (Katkılarım) and the replies of one root comment.
        builder.HasIndex(c => new { c.UserId, c.CreatedAt });
        builder.HasIndex(c => c.ParentCommentId);

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(c => c.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // A comment is the reader's, so it goes with the reader's account — the usual cascade;
        // its replies go with it (a reply without its root is nothing to show).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<BlogComment>()
            .WithMany()
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
