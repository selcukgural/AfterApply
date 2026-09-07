using AfterApply.Domain.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Domain.Documents;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.Identity;
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
        builder.Property(a => a.HrName).HasMaxLength(200);
        // 320 is the RFC-maximum length of an email address (64 local + @ + 255 domain).
        builder.Property(a => a.HrEmail).HasMaxLength(320);
        builder.Property(a => a.HrLinkedInUrl).HasMaxLength(500);
        builder.Property(a => a.HrEmailSource).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.EmploymentType).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Source).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.UserId, a.Status });

        // Cascade from the account. Nothing else deletes these rows: the domain holds a plain Guid
        // UserId (DECISIONS.md, "Domain does not model User"), so before this FK existed the only
        // thing standing between a deleted account and its leftover data was a hand-written list of
        // ExecuteDelete calls in DeleteAccountAsync — and that list had already drifted, silently
        // orphaning every TrackedJob, Reminder and EmailSuggestion the user ever had.
        //
        // The FK is a shadow one: no navigation property, so the domain still knows nothing about
        // ApplicationUser. Same shape RefreshTokens, PersonalAccessTokens and EmailConnections have
        // used all along — this only extends it to the tables that were missed.
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(a => a.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Job>()
            .WithMany()
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.SetNull);

        // SetNull, not Cascade: deleting a CV file must not delete the applications that were sent
        // with it. See Application.CvDocumentId.
        builder.HasOne<CvDocument>()
            .WithMany()
            .HasForeignKey(a => a.CvDocumentId)
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
