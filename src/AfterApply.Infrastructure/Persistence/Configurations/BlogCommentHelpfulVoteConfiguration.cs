using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogCommentHelpfulVoteConfiguration : IEntityTypeConfiguration<BlogCommentHelpfulVote>
{
    public void Configure(EntityTypeBuilder<BlogCommentHelpfulVote> builder)
    {
        builder.ToTable("BlogCommentHelpfulVotes");
        builder.HasKey(v => v.Id);

        builder.HasIndex(v => new { v.CommentId, v.UserId }).IsUnique();

        builder.HasOne<BlogComment>()
            .WithMany()
            .HasForeignKey(v => v.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
