using AfterApply.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> builder)
    {
        builder.ToTable("ApplicationEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Metadata).HasColumnType("jsonb");

        builder.HasIndex(e => e.ApplicationId);
    }
}
