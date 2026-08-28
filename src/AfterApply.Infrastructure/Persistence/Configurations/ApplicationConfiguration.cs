using AfterApply.Domain.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainApplication = AfterApply.Domain.Applications.Application;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<DomainApplication>
{
    public void Configure(EntityTypeBuilder<DomainApplication> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.JobTitle).IsRequired().HasMaxLength(300);
        builder.Property(a => a.JobUrl).HasMaxLength(2000);
        builder.Property(a => a.Location).HasMaxLength(200);
        builder.Property(a => a.Notes).HasColumnType("text");
        builder.Property(a => a.EmploymentType).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.UserId, a.Status });

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(a => a.Events)
            .WithOne()
            .HasForeignKey(e => e.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Events).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(a => a.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.StatusHistory).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
