using AfterApply.Domain.Imports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ImportRowErrorConfiguration : IEntityTypeConfiguration<ImportRowError>
{
    public void Configure(EntityTypeBuilder<ImportRowError> builder)
    {
        builder.ToTable("ImportRowErrors");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.RawRow).IsRequired().HasColumnType("text");
        builder.Property(e => e.ErrorMessage).IsRequired().HasMaxLength(1000);

        builder.HasIndex(e => e.ImportBatchId);
    }
}
