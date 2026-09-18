using AfterApply.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class CvDocumentScanConfiguration : IEntityTypeConfiguration<CvDocumentScan>
{
    public void Configure(EntityTypeBuilder<CvDocumentScan> builder)
    {
        builder.ToTable("CvDocumentScans");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ReportJson).IsRequired().HasColumnType("jsonb");

        // One report per document, and the document is the only way in — every read is "the scan
        // of this CV of this user", through the document's own ownership check.
        builder.HasIndex(s => s.CvDocumentId).IsUnique();

        // Deleted with the CV, which is deleted with the account: the report is a description of
        // the file and has no meaning without it.
        builder.HasOne<CvDocument>()
            .WithMany()
            .HasForeignKey(s => s.CvDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
