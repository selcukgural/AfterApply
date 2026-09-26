using AfterApply.Domain.Blog;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class BlogPostConfiguration : IEntityTypeConfiguration<BlogPost>
{
    public void Configure(EntityTypeBuilder<BlogPost> builder)
    {
        builder.ToTable("BlogPosts");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Language).HasMaxLength(BlogLanguage.MaxLength);
        // Blog or guide (2026-09-26). Existing rows are blog posts, which the migration's default says.
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(20).HasDefaultValue(BlogPostKind.Blog);
        // A string, as every enum column in this schema (see CompanyReviewConfiguration).
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Slug).HasMaxLength(BlogSlugGenerator.MaxLength);

        builder.Property(p => p.DraftTitle).HasMaxLength(BlogPost.MaxTitleLength);
        builder.Property(p => p.DraftExcerpt).HasMaxLength(BlogPost.MaxExcerptLength);
        builder.Property(p => p.Title).HasMaxLength(BlogPost.MaxTitleLength);
        builder.Property(p => p.Excerpt).HasMaxLength(BlogPost.MaxExcerptLength);
        // The SEO fields (2026-09-21), one set per slot. The keyword lists are text[] — never
        // queried, only carried to the page.
        builder.Property(p => p.DraftSeoTitle).HasMaxLength(BlogSeo.MaxSeoTitleLength);
        builder.Property(p => p.SeoTitle).HasMaxLength(BlogSeo.MaxSeoTitleLength);
        builder.Property(p => p.DraftPrimaryKeyword).HasMaxLength(BlogSeo.MaxKeywordLength);
        builder.Property(p => p.PrimaryKeyword).HasMaxLength(BlogSeo.MaxKeywordLength);
        builder.Property(p => p.DraftCoverAlt).HasMaxLength(BlogSeo.MaxCoverAltLength);
        builder.Property(p => p.CoverAlt).HasMaxLength(BlogSeo.MaxCoverAltLength);
        // The editor's documents as jsonb: not queried today, but a jsonb column is validated on
        // write (a truncated document cannot be stored) and is what a later query would want.
        builder.Property(p => p.DraftContentJson).HasColumnType("jsonb");
        builder.Property(p => p.PublishedContentJson).HasColumnType("jsonb");

        // The guide's related list: ids only, never queried by — the public read resolves them.
        builder.Property(p => p.DraftRelatedPostIds).HasColumnType("uuid[]");
        builder.Property(p => p.RelatedPostIds).HasColumnType("uuid[]");

        // The public URL: one slug per kind and language (/blog/x and /guide/x are different
        // pages, 2026-09-26). Filtered because a never-published draft has no
        // slug yet. The name carries "Slug" on purpose — BlogAdminService retries a publish on a
        // 23505 whose constraint name says so (the CompanySlugAllocator convention).
        builder.HasIndex(p => new { p.Kind, p.Language, p.Slug })
            .IsUnique()
            .HasDatabaseName("IX_BlogPosts_Kind_Language_Slug")
            .HasFilter("\"Slug\" IS NOT NULL");
        // The public list: published posts of one kind and language, newest first.
        builder.HasIndex(p => new { p.Kind, p.Status, p.Language, p.PublishedAt });
        // The author's own drafts in the admin table.
        builder.HasIndex(p => p.AuthorUserId);

        // Not the cascade every user-owned table uses (ApplicationConfiguration explains that
        // convention): a post is site content, not a user's data. When an admin's account is
        // deleted the post stays, with no author — a published one keeps its URL, a draft
        // becomes nobody's and is visible to nobody, which is the honest state for a draft whose
        // author is gone (DECISIONS.md 2026-09-19).
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.AuthorUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // The other-language twin. Deleting one side unlinks the other rather than deleting it.
        builder.HasOne<BlogPost>()
            .WithMany()
            .HasForeignKey(p => p.TranslationOfPostId)
            .OnDelete(DeleteBehavior.SetNull);

        // The cover is one of the post's own media rows; if it goes, the post just has no cover.
        // (Media cascades from the post the other way — BlogMediaConfiguration — and Postgres is
        // fine with the two directions because neither cycles on delete.)
        builder.HasOne<BlogMedia>()
            .WithMany()
            .HasForeignKey(p => p.CoverMediaId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
