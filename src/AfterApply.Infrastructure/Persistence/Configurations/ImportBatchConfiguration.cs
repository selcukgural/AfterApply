using AfterApply.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatches");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.FileName).IsRequired().HasMaxLength(500);

        builder.HasIndex(b => b.UserId);

        builder.HasMany(b => b.RowErrors)
            .WithOne()
            .HasForeignKey(e => e.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(b => b.RowErrors).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
