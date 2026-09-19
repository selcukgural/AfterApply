using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogPostLikeConfiguration : IEntityTypeConfiguration<BlogPostLike>
{
    public void Configure(EntityTypeBuilder<BlogPostLike> builder)
    {
        builder.ToTable("BlogPostLikes");
        builder.HasKey(l => l.Id);

        builder.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(l => l.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // A like is the reader's, so it goes with the reader's account — the usual cascade.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
