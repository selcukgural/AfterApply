using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CvDocumentConfiguration : IEntityTypeConfiguration<CvDocument>
{
    public void Configure(EntityTypeBuilder<CvDocument> builder)
    {
        builder.ToTable("CvDocuments");
        builder.HasKey(d => d.Id);

        // Matches CvFileRules.SanitizeFileName's own cap — the sanitizer is what guarantees this
        // never truncates at the database.
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(200);
        // cvs/{guid}/{guid}.docx is 84 characters; the headroom is for a later layout change.
        builder.Property(d => d.StorageObjectName).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Format).HasConversion<string>().IsRequired().HasMaxLength(50);

        // The only index this table needs: every query is "this user's CVs", and there are at most
        // ten of them. The one-default invariant is held by the advisory lock in CvDocumentService
        // rather than by a unique index — a unique filtered index is checked per statement, and EF
        // does not promise to send "clear the old default" before "set the new one" within a single
        // SaveChanges, so a legitimate swap could fail on statement order alone.
        builder.HasIndex(d => d.UserId);

        // Cascade from the account — see ApplicationConfiguration for why this is a foreign key
        // and not a line in DeleteAccountAsync.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
