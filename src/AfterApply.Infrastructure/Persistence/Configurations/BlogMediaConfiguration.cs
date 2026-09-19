using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogMediaConfiguration : IEntityTypeConfiguration<BlogMedia>
{
    public void Configure(EntityTypeBuilder<BlogMedia> builder)
    {
        builder.ToTable("BlogMedia");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ObjectName).HasMaxLength(200);
        builder.Property(m => m.ContentType).HasMaxLength(50);

        // The post's images, collected before the post is deleted so the objects can follow.
        builder.HasIndex(m => m.PostId);

        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(m => m.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        // The image belongs to the post, not to whoever uploaded it — see BlogPostConfiguration.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(m => m.UploaderUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
