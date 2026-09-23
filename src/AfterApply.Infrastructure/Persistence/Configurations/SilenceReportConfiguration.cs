using AfterApply.Domain.Companies;
using AfterApply.Domain.SilenceReports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterApply.Infrastructure.Persistence.Configurations;

public sealed class SilenceReportConfiguration : IEntityTypeConfiguration<SilenceReport>
{
    public void Configure(EntityTypeBuilder<SilenceReport> builder)
    {
        builder.ToTable("SilenceReports");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Stage).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Wait).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Locale).HasMaxLength(8).IsRequired();
        builder.Property(r => r.Source).HasConversion<string>().HasMaxLength(32);

        // A report is about a company; if the company row ever goes, so do its reports.
        builder.HasOne<Company>().WithMany().HasForeignKey(r => r.CompanyId).OnDelete(DeleteBehavior.Cascade);

        // The one read: a company's reports inside the window.
        builder.HasIndex(r => new { r.CompanyId, r.SubmittedAt });

        // No foreign key to Users, because there is no UserId — see SilenceReport's summary.
    }
}
